"""CMU14: Post the published test merges to the player-facing Discord changelog."""

import json
import os
import re

from actions_changelogs_since_last_run import create_change_line, send_message_lines


def main():
    pulls = json.loads(os.environ["MERGED_JSON"])
    if not pulls:
        return
    if not os.environ.get("DISCORD_WEBHOOK_URL"):
        raise SystemExit("Configure CHANGELOG_DISCORD_WEBHOOK to announce test merges.")

    lines = [
        "**Test merges published for the main server**\n",
        "Try these changes when the server updates. Report bugs with `bughelp` in the in-game console.\n",
    ]
    for pull in pulls:
        # Display PR titles as plain text inside the Discord message.
        title = re.sub(r"([\\`*_~|\[\]<>])", r"\\\1", " ".join(pull["title"].split()))
        lines.append(create_change_line("🧪", title, pull["url"]))
    send_message_lines(lines)


if __name__ == "__main__":
    main()
