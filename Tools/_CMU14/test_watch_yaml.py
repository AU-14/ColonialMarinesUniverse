import contextlib
import io
import json
import os
import signal
import sys
import unittest
from unittest import mock
from urllib.error import URLError

import watch_yaml

ENV = dict(CI_YAML_TOKEN="t", GITHUB_API_URL="https://api.github.com", GITHUB_REPOSITORY="o/r",
           GITHUB_RUN_ID="1", GITHUB_RUN_ATTEMPT="1")
FAILED = dict(name="YAML Linter", status="completed", conclusion="failure")
PASSED = dict(name="YAML Linter", status="completed", conclusion="success")
RUNNING = dict(name="YAML Linter", status="in_progress", conclusion=None)


class Reply:
    def __init__(self, jobs, link=""):
        self.body = json.dumps({"jobs": jobs}).encode()
        self.headers = {"Link": link}

    def read(self, *args):
        body, self.body = self.body, b""
        return body

    def __enter__(self):
        return self

    def __exit__(self, *args):
        return False


def run(replies, argv, kill):
    """Run the watcher against queued replies; return its exit code and the sleep calls."""
    sleeps = []
    with mock.patch.dict(os.environ, ENV), mock.patch.object(sys, "argv", ["watch_yaml.py", *argv]), \
            mock.patch.object(watch_yaml, "urlopen", side_effect=replies) as fetch, \
            mock.patch.object(watch_yaml.os, "kill", kill), mock.patch.object(watch_yaml.time, "sleep", sleeps.append), \
            contextlib.redirect_stdout(io.StringIO()):
        code = watch_yaml.main()
    return code, sleeps, fetch.call_args_list


class WatchYamlTests(unittest.TestCase):
    def test_failed_job_stops_the_runner(self):
        kill = mock.Mock()
        self.assertEqual(run([Reply([FAILED])], ["4242"], kill), (None, [], [mock.ANY]))
        kill.assert_called_once_with(4242, signal.SIGTERM)

    def test_completed_job_without_success_stops_the_runner(self):
        for conclusion in ("timed_out", "cancelled", "failure"):
            with self.subTest(conclusion):
                kill = mock.Mock()
                job = dict(name="YAML Linter", status="completed", conclusion=conclusion)
                run([Reply([job])], ["4242"], kill)
                kill.assert_called_once_with(4242, signal.SIGTERM)

    def test_pending_job_is_polled_until_success_without_stopping(self):
        kill = mock.Mock()
        code, sleeps, fetches = run([Reply([RUNNING]), Reply([PASSED])], ["4242"], kill)
        self.assertEqual((code, sleeps), (None, [watch_yaml.POLL]))
        self.assertEqual(len(fetches), 2)
        kill.assert_not_called()

    def test_unavailable_status_backs_off_and_keeps_polling(self):
        kill = mock.Mock()
        code, sleeps, _ = run([URLError("boom"), Reply([FAILED])], ["4242"], kill)
        self.assertEqual(sleeps, [watch_yaml.POLL * 2])
        kill.assert_called_once_with(4242, signal.SIGTERM)

    def test_ambiguous_job_never_stops_the_runner(self):
        kill = mock.Mock()
        code, sleeps, _ = run([Reply([]), Reply([FAILED, FAILED]), Reply([PASSED])], ["4242"], kill)
        self.assertEqual(sleeps, [watch_yaml.POLL] * 2)
        kill.assert_not_called()

    def test_follows_the_next_page(self):
        kill = mock.Mock()
        link = '<https://api.github.com/repo/jobs?page=2>; rel="next", <https://api.github.com/repo/jobs?page=2>; rel="last"'
        code, sleeps, fetches = run([Reply([], link), Reply([FAILED])], ["4242"], kill)
        self.assertEqual(fetches[1].args[0].full_url, "https://api.github.com/repo/jobs?page=2")
        kill.assert_called_once_with(4242, signal.SIGTERM)

    def test_check_fails_only_on_an_affirmative_failure(self):
        self.assertEqual(run([Reply([FAILED])], ["--check"], mock.Mock())[0], 1)
        self.assertEqual(run([Reply([PASSED])], ["--check"], mock.Mock())[0], 0)
        self.assertEqual(run([Reply([])], ["--check"], mock.Mock())[0], 0)
        self.assertEqual(run([URLError("boom")], ["--check"], mock.Mock())[0], 0)


if __name__ == "__main__":
    unittest.main()
