using System.Collections.Immutable;


namespace Content.Server.Voting
{
    public sealed class VoteFinishedEventArgs : EventArgs
    {
        /// <summary>
        ///     Null if stalemate.
        /// </summary>
        public readonly object? Winner;

        /// <summary>
        ///     Winners. More than one if there was a stalemate.
        /// </summary>
        public readonly ImmutableArray<object> Winners;

        /// <summary>
        ///     Stores all the votes in a string, for webhooks. 
        /// </summary>
        public readonly List<int> Votes;

        /// <summary>
        ///     Winner chosen by a caller after resolving a tie.
        /// </summary>
        public object? SelectedWinner { get; private set; }

        public VoteFinishedEventArgs(object? winner, ImmutableArray<object> winners, List<int> votes)
        {
            Winner = winner;
            Winners = winners;
            Votes = votes;
            SelectedWinner = winner;
        }

        public void ResolveWinner(object winner)
        {
            SelectedWinner = winner;
        }
    }
}
