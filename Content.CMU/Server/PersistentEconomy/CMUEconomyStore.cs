using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Content.Server.CMU14.PersistentEconomy;

/// <summary>
/// The sole writer of persistent money. Each command, its ledger entries and its
/// round limits commit in one SQLite transaction. The database is server-local.
/// </summary>
public sealed class CMUEconomyStore : IDisposable
{
    public const long MaximumBalance = 1_000_000_000_000;
    private readonly SqliteConnection _connection;
    private readonly object _gate = new();

    public CMUEconomyStore(string path)
    {
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        _connection.Open();
        Execute(null, "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000;");
        Execute(null, """
            CREATE TABLE IF NOT EXISTS cmu_accounts (player TEXT PRIMARY KEY, data TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS cmu_rounds (player TEXT NOT NULL, round INTEGER NOT NULL, data TEXT NOT NULL, PRIMARY KEY(player, round));
            CREATE TABLE IF NOT EXISTS cmu_operations (key TEXT PRIMARY KEY, timestamp TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS cmu_ledger (
                id TEXT PRIMARY KEY, player TEXT NOT NULL, round INTEGER NOT NULL,
                amount INTEGER NOT NULL, before_balance INTEGER NOT NULL, after_balance INTEGER NOT NULL,
                type TEXT NOT NULL, description TEXT NOT NULL, timestamp TEXT NOT NULL,
                related_player TEXT, operation_key TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS cmu_ledger_player_round ON cmu_ledger(player, round);
            CREATE TABLE IF NOT EXISTS cmu_metrics (key TEXT PRIMARY KEY, round INTEGER NOT NULL, type TEXT NOT NULL, amount INTEGER NOT NULL);
            """);
    }

    public sealed class Account
    {
        public long Balance { get; set; }
        public long LifetimeEarned { get; set; }
        public long LifetimeSpent { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Dictionary<string, Purchase> Purchases { get; set; } = new();
        public bool StakeEnabled { get; set; } = true;
    }

    public sealed class Purchase
    {
        public long PricePaid { get; set; }
        public DateTime PurchasedAt { get; set; }
    }

    public sealed class RoundState
    {
        public bool DeploymentIssued { get; set; }
        public bool Settled { get; set; }
        public long Stake { get; set; }
        public long SettlementCap { get; set; }
        /// <summary>Cash deposited during the round but not yet persistent.</summary>
        public long RoundEscrow { get; set; }
        /// <summary>Amount actually returned to the persistent bank by settlement.</summary>
        public long CashCredited { get; set; }
        public long StartingBalance { get; set; }
        public string Job { get; set; } = "";
    }

    public sealed record Entry(long Amount, long Before, long After, string Type, string Description, string Timestamp);

    public Account ReadAccount(Guid player)
    {
        lock (_gate)
            return Read<Account>(null, "SELECT data FROM cmu_accounts WHERE player=$p", player, 0);
    }

    public RoundState ReadRound(Guid player, int round)
    {
        lock (_gate)
            return Read<RoundState>(null, "SELECT data FROM cmu_rounds WHERE player=$p AND round=$r", player, round);
    }

    public List<Guid> RoundPlayers(int round)
    {
        lock (_gate)
        {
            using var cmd = Command(null, "SELECT player FROM cmu_rounds WHERE round=$r", ("$r", round));
            using var reader = cmd.ExecuteReader();
            var players = new List<Guid>();
            while (reader.Read())
            {
                if (Guid.TryParse(reader.GetString(0), out var player))
                    players.Add(player);
            }
            return players;
        }
    }

    public List<Entry> History(Guid player, int? round = null)
    {
        lock (_gate)
        {
            using var cmd = Command(null, "SELECT amount,before_balance,after_balance,type,description,timestamp FROM cmu_ledger WHERE player=$p" +
                (round == null ? "" : " AND round=$r") + " ORDER BY rowid DESC LIMIT 100", ("$p", player.ToString()), ("$r", round ?? 0));
            using var reader = cmd.ExecuteReader();
            var entries = new List<Entry>();
            while (reader.Read())
                entries.Add(new Entry(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
            return entries;
        }
    }

    public bool Mutate(Guid player, int round, string key, Func<Operation, bool> action)
    {
        if (player == Guid.Empty || round < 0 || string.IsNullOrWhiteSpace(key) || key.Length > 250)
            return false;
        lock (_gate)
        {
            using var transaction = _connection.BeginTransaction();
            using (var exists = Command(transaction, "SELECT 1 FROM cmu_operations WHERE key=$k", ("$k", key)))
            {
                if (exists.ExecuteScalar() != null)
                    return false;
            }

            var op = new Operation(this, transaction, player, round, key);
            if (!action(op))
                return false;
            op.Save();
            Execute(transaction, "INSERT INTO cmu_operations VALUES ($k,$t)", ("$k", key), ("$t", DateTime.UtcNow.ToString("O")));
            transaction.Commit();
            return true;
        }
    }

    public sealed class Operation
    {
        private readonly CMUEconomyStore _store;
        private readonly SqliteTransaction _transaction;
        private readonly Guid _player;
        private readonly int _round;
        private readonly string _key;
        private readonly Dictionary<Guid, Account> _accounts = new();
        public Account Account => GetAccount(_player);
        public RoundState Round { get; }

        internal Operation(CMUEconomyStore store, SqliteTransaction transaction, Guid player, int round, string key)
        {
            _store = store;
            _transaction = transaction;
            _player = player;
            _round = round;
            _key = key;
            Round = store.Read<RoundState>(transaction, "SELECT data FROM cmu_rounds WHERE player=$p AND round=$r", player, round);
        }

        private Account GetAccount(Guid player)
        {
            if (!_accounts.TryGetValue(player, out var account))
                _accounts[player] = account = _store.Read<Account>(_transaction, "SELECT data FROM cmu_accounts WHERE player=$p", player, 0);
            return account;
        }

        public bool Change(long amount, string type, string description, Guid? related = null, Guid? owner = null)
        {
            if (amount == 0 || amount < -MaximumBalance || amount > MaximumBalance || description.Length > 512)
                return false;
            var player = owner ?? _player;
            var account = GetAccount(player);
            var before = account.Balance;
            if (amount > 0 && before > MaximumBalance - amount ||
                amount < 0 && before < -amount)
                return false;

            // Bank/cash conversions are capital movements, not earnings or spending.
            if (type is not ("RoundStake" or "CashWithdrawal" or "CashDeposit" or "CashSettlement"))
            {
                if (amount > 0 && account.LifetimeEarned > long.MaxValue - amount ||
                    amount < 0 && account.LifetimeSpent > long.MaxValue + amount)
                    return false;
            }

            var after = before + amount;
            account.Balance = after;
            account.UpdatedAt = DateTime.UtcNow;
            if (type is not ("RoundStake" or "CashWithdrawal" or "CashDeposit" or "CashSettlement"))
            {
                if (amount > 0)
                    account.LifetimeEarned += amount;
                else
                    account.LifetimeSpent -= amount;
            }
            _store.Execute(_transaction, "INSERT INTO cmu_ledger VALUES ($id,$p,$r,$a,$b,$c,$type,$d,$t,$related,$k)",
                ("$id", Guid.NewGuid().ToString()), ("$p", player.ToString()), ("$r", _round), ("$a", amount),
                ("$b", before), ("$c", after), ("$type", type), ("$d", description), ("$t", account.UpdatedAt.ToString("O")),
                ("$related", related?.ToString() ?? (object) DBNull.Value), ("$k", _key));
            return true;
        }

        public bool Transfer(Guid recipient, long amount)
        {
            return recipient != Guid.Empty && recipient != _player && amount > 0 &&
                   Change(-amount, "PlayerTransferOut", "Player transfer", recipient) &&
                   Change(amount, "PlayerTransferIn", "Player transfer", _player, recipient);
        }

        public bool Deploy(long cost, int stakePercent, long stakeCap, int multiplierPercent, string job)
        {
            if (Round.DeploymentIssued || Round.Settled || cost < 0 || stakePercent is < 0 or > 100 ||
                stakeCap < 0 || multiplierPercent is < 0 or > 1000)
                return false;
            Round.StartingBalance = Account.Balance;
            if (cost > 0 && !Change(-cost, "LoadoutDeployment", "Personal loadout"))
                return false;
            var stake = Account.StakeEnabled ? Account.Balance * stakePercent / 100 : 0;
            if (stakeCap > 0)
                stake = Math.Min(stake, stakeCap);
            if (stake > int.MaxValue || stake > 0 && !Change(-stake, "RoundStake", "Deployment cash"))
                return false;
            Round.Stake = stake;
            Round.SettlementCap = stake * multiplierPercent / 100;
            Round.DeploymentIssued = true;
            Round.Job = job;
            return true;
        }

        public bool DepositToEscrow(long cash)
        {
            if (!Round.DeploymentIssued || Round.Settled || cash <= 0)
                return false;

            var remaining = Round.SettlementCap - Round.CashCredited - Round.RoundEscrow;
            if (cash > remaining)
                return false;

            Round.RoundEscrow = checked(Round.RoundEscrow + cash);
            return true;
        }

        public bool WithdrawEscrow(long cash)
        {
            if (!Round.DeploymentIssued || Round.Settled || cash <= 0 || cash > Round.RoundEscrow)
                return false;

            Round.RoundEscrow -= cash;
            return true;
        }

        public bool Settle(long carriedCash)
        {
            if (!Round.DeploymentIssued || Round.Settled || carriedCash < 0)
                return false;

            var capacity = Math.Max(0, Round.SettlementCap - Round.CashCredited);
            var eligible = checked(Round.RoundEscrow + carriedCash);
            var settlement = Math.Min(eligible, capacity);

            if (settlement > 0 && !Change(settlement, "CashSettlement", "End of round settlement"))
                return false;

            Round.CashCredited = checked(Round.CashCredited + settlement);
            Round.RoundEscrow = 0;
            Round.Settled = true;
            return true;
        }

        public bool ForfeitRound()
        {
            if (!Round.DeploymentIssued || Round.Settled)
                return false;

            Round.RoundEscrow = 0;
            Round.Settled = true;
            return true;
        }

        public bool Buy(string item, long price)
        {
            if (price < 0 || Account.Purchases.ContainsKey(item) || price > 0 && !Change(-price, "PermanentPurchase", item))
                return false;
            Account.Purchases.Add(item, new Purchase { PricePaid = price, PurchasedAt = DateTime.UtcNow });
            return true;
        }

        internal void Save()
        {
            foreach (var (player, account) in _accounts)
                _store.Execute(_transaction, "INSERT INTO cmu_accounts VALUES ($p,$d) ON CONFLICT(player) DO UPDATE SET data=$d",
                    ("$p", player.ToString()), ("$d", JsonSerializer.Serialize(account)));
            _store.Execute(_transaction, "INSERT INTO cmu_rounds VALUES ($p,$r,$d) ON CONFLICT(player,round) DO UPDATE SET data=$d",
                ("$p", _player.ToString()), ("$r", _round), ("$d", JsonSerializer.Serialize(Round)));
        }
    }

    public string Statistics()
    {
        lock (_gate)
        {
            using var cmd = Command(null, "SELECT data FROM cmu_accounts");
            using var reader = cmd.ExecuteReader();
            var balances = new List<long>();
            while (reader.Read())
                balances.Add(JsonSerializer.Deserialize<Account>(reader.GetString(0))!.Balance);
            balances.Sort();
            if (balances.Count == 0)
                return "Accounts: 0";
            long Percentile(decimal p) => balances[(int) Math.Ceiling((balances.Count - 1) * p)];
            reader.Close();
            using var totals = Command(null, "SELECT type, SUM(amount) FROM cmu_ledger GROUP BY type");
            using var totalReader = totals.ExecuteReader();
            var result = $"Accounts: {balances.Count}; total: {balances.Sum()}; average: {balances.Average(b => (decimal) b):F0}; median: {Percentile(.5m)}; P90: {Percentile(.9m)}; P99: {Percentile(.99m)}";
            while (totalReader.Read())
                result += $"\n{totalReader.GetString(0)}: {totalReader.GetInt64(1)}";
            totalReader.Close();
            using var rounds = Command(null, "SELECT data FROM cmu_rounds");
            using var roundReader = rounds.ExecuteReader();
            var completed = new List<RoundState>();
            while (roundReader.Read())
            {
                var round = JsonSerializer.Deserialize<RoundState>(roundReader.GetString(0))!;
                if (round.DeploymentIssued && round.Settled)
                    completed.Add(round);
            }
            if (completed.Count > 0)
            {
                result += $"\nCompleted deployments: {completed.Count}; average stake: {completed.Average(r => (decimal) r.Stake):F2}" +
                          $"; average cash returned: {completed.Average(r => (decimal) r.CashCredited):F2}" +
                          $"; average cash profit: {completed.Average(r => (decimal) (r.CashCredited - r.Stake)):F2}";
                var staked = completed.Where(r => r.Stake > 0).ToArray();
                if (staked.Length > 0)
                    result += $"\nCash profit / stake: {staked.Average(r => (decimal) (r.CashCredited - r.Stake) / r.Stake):P2}";
                var capped = completed.Where(r => r.SettlementCap > 0).ToArray();
                if (capped.Length > 0)
                    result += $"; cap utilization: {capped.Average(r => (decimal) r.CashCredited / r.SettlementCap):P2}";
            }
            roundReader.Close();
            using var metrics = Command(null, "SELECT type,SUM(amount) FROM cmu_metrics GROUP BY type");
            using var metricReader = metrics.ExecuteReader();
            while (metricReader.Read())
                result += $"\n{metricReader.GetString(0)}: {metricReader.GetInt64(1)}";
            return result;
        }
    }

    public void RecordMetric(string key, int round, string type, long amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        lock (_gate)
            Execute(null, "INSERT OR IGNORE INTO cmu_metrics VALUES ($k,$r,$t,$a)",
                ("$k", key), ("$r", round), ("$t", type), ("$a", amount));
    }

    private T Read<T>(SqliteTransaction? transaction, string sql, Guid player, int round) where T : new()
    {
        using var cmd = Command(transaction, sql, ("$p", player.ToString()), ("$r", round));
        var value = cmd.ExecuteScalar();
        return value is string json ? JsonSerializer.Deserialize<T>(json)! : new T();
    }

    private SqliteCommand Command(SqliteTransaction? transaction, string sql, params (string Name, object Value)[] values)
    {
        var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in values)
            command.Parameters.AddWithValue(name, value);
        return command;
    }

    private void Execute(SqliteTransaction? transaction, string sql, params (string Name, object Value)[] values)
    {
        using var command = Command(transaction, sql, values);
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
