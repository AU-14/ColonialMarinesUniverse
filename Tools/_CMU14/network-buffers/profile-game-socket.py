#!/usr/bin/env python3
"""Bounded, read-only Linux game-socket sampling; no packet payloads or addresses."""

import argparse
import datetime
import json
import pathlib
import re
import subprocess
import time


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def process_start(path):
    # comm can contain spaces and parentheses; field 22 follows its closing ')'.
    return int((path / "stat").read_text().rsplit(")", 1)[1].split()[19])


def owned_sockets(path):
    result = set()
    for fd in (path / "fd").iterdir():
        try:
            target = str(fd.readlink())
        except FileNotFoundError:
            continue
        if target.startswith("socket:["):
            result.add(target[8:-1])
    return result


def socket_rows(path, owned, port):
    result = {}
    for protocol in ("udp", "udp6"):
        for line in (path / "net" / protocol).read_text().splitlines()[1:]:
            fields = line.split()
            if len(fields) < 13 or fields[9] not in owned:
                continue
            if int(fields[1].split(":")[1], 16) != port:
                continue
            tx, rx = fields[4].split(":")
            result[fields[9]] = {
                "protocol": protocol,
                "rxQueueBytes": int(rx, 16),
                "txQueueBytes": int(tx, 16),
                "droppedDatagrams": int(fields[-1]),
            }
    return result


def udp_counters(path):
    lines = (path / "net/snmp").read_text().splitlines()
    for index, line in enumerate(lines):
        if line.startswith("Udp:"):
            return dict(zip(line.split()[1:], map(int, lines[index + 1].split()[1:])))
    raise RuntimeError("IPv4 UDP namespace counters unavailable")


def socket_memory(pid, inodes, port):
    result = {}
    output = subprocess.check_output(
        ["nsenter", "-t", str(pid), "-n", "ss", "-u", "-a", "-n", "-m", "-e",
         f"( sport = :{port} )"], text=True, timeout=10)
    inode = None
    for line in output.splitlines():
        match = re.search(r"ino:(\d+)", line)
        if match:
            inode = match[1]
        memory = re.search(r"skmem:\(([^)]+)\)", line)
        if inode in inodes and memory:
            result[inode] = {key: int(value) for key, value in
                             re.findall(r"([a-z]+)(\d+)", memory[1])}
    return result


def thread_sample(path):
    fields = (path / "stat").read_text().rsplit(")", 1)[1].split()
    running, waiting, slices = map(int, (path / "schedstat").read_text().split())
    return {"state": fields[0], "runNs": running, "runQueueNs": waiting, "slices": slices}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seconds", type=int, choices=range(5, 121), default=60, metavar="5..120")
    parser.add_argument("--interval-ms", type=int, choices=range(50, 1001), default=50, metavar="50..1000")
    parser.add_argument("--port", type=int, default=1212)
    args = parser.parse_args()
    if not 1 <= args.port <= 65535:
        parser.error("port must be 1..65535")

    identity = json.loads(subprocess.check_output(["cmu-perf", "status"], text=True, timeout=30))
    pid = identity["host_pid"]
    path = pathlib.Path("/proc") / str(pid)
    start_ticks = identity["start_ticks"]
    if process_start(path) != start_ticks:
        raise RuntimeError("Game process changed before sampling")
    inodes = set(socket_rows(path, owned_sockets(path), args.port))
    if not inodes:
        raise RuntimeError("No game-owned UDP sockets on the requested port")
    threads = {str(pid): "game-main"}
    for thread in (path / "task").iterdir():
        name = (thread / "comm").read_text().strip()
        if "lidgren" in name.lower():
            threads[thread.name] = name
    result = {
        "schema": 1, "startedUtc": utc_now(), "hostPid": pid,
        "startTicks": start_ticks, "bootId": identity["boot_id"], "port": args.port,
        "intervalMs": args.interval_ms, "threads": threads,
        "socketMemoryBefore": socket_memory(pid, inodes, args.port),
        "counterScope": "socket drops/queues; IPv4 UDP counters cover the whole network namespace",
        "samples": [],
    }
    interval = args.interval_ms / 1000
    started = time.monotonic()
    cpu_started = time.process_time()
    next_sample = started
    next_ownership_check = started
    failure = None
    try:
        while True:
            now = time.monotonic()
            if process_start(path) != start_ticks:
                raise RuntimeError("Game process identity changed")
            if now >= next_ownership_check:
                if not inodes.issubset(owned_sockets(path)):
                    raise RuntimeError("Game socket ownership changed")
                next_ownership_check = now + 1
            sockets = socket_rows(path, inodes, args.port)
            if set(sockets) != inodes:
                raise RuntimeError("Game socket disappeared")
            result["samples"].append({
                "elapsedMs": (now - started) * 1000,
                "sockets": sockets,
                "udp": udp_counters(path),
                "threads": {tid: thread_sample(path / "task" / tid) for tid in threads},
                "readMs": (time.monotonic() - now) * 1000,
            })
            if now - started >= args.seconds:
                break
            next_sample += interval
            # Skip missed sample slots rather than bursting reads after a scheduling stall.
            next_sample = max(next_sample, time.monotonic())
            time.sleep(max(0, next_sample - time.monotonic()))
        result["socketMemoryAfter"] = socket_memory(pid, inodes, args.port)
    except (OSError, RuntimeError, subprocess.SubprocessError) as error:
        failure = str(error)
    result["samplerCpuMs"] = (time.process_time() - cpu_started) * 1000
    result["endedUtc"] = utc_now()
    result["complete"] = failure is None
    if failure:
        result["error"] = failure
    print(json.dumps(result, separators=(",", ":")))
    return 0 if failure is None else 1


if __name__ == "__main__":
    raise SystemExit(main())
