const assert = require('node:assert/strict');
const { test } = require('node:test');

const {
  parsePullRequestMetadata,
  normalizePullRequestTargets,
  shouldProcessPullRequest,
  shouldProcessPullRequestHead,
} = require('./ci-status-labels');

test('uses artifact metadata as the authoritative PR head', () => {
  const targets = parsePullRequestMetadata('{"number":42,"head_sha":"abc123","head_ref":"feature","head_repo_full_name":"owner/repo"}');

  assert.deepEqual(targets, [
    {
      number: 42,
      expectedHeadSha: 'abc123',
      expectedHeadRef: 'feature',
      expectedHeadRepoFullName: 'owner/repo',
      source: 'artifact',
    },
  ]);
});

test('does not use workflow_run head_sha as a PR head fallback', () => {
  const targets = normalizePullRequestTargets([{ number: 7 }], 'workflow_run');

  assert.deepEqual(targets, [
    {
      number: 7,
      expectedHeadSha: null,
      expectedHeadRef: null,
      expectedHeadRepoFullName: null,
      source: 'workflow_run',
    },
  ]);
});

test('skips stale runs only when the expected PR head is known', () => {
  assert.equal(shouldProcessPullRequestHead('branch-head', null), true);
  assert.equal(shouldProcessPullRequestHead('branch-head', 'branch-head'), true);
  assert.equal(shouldProcessPullRequestHead('branch-head', 'old-branch-head'), false);
});

test('requires artifact PR targets to match the workflow run branch and repository', () => {
  const run = {
    head_branch: 'feature',
    head_repository: {
      full_name: 'owner/repo',
    },
  };
  const latest = {
    head: {
      ref: 'feature',
      repo: {
        full_name: 'owner/repo',
      },
      sha: 'branch-head',
    },
  };

  assert.equal(
    shouldProcessPullRequest(latest, {
      expectedHeadSha: 'branch-head',
      expectedHeadRef: 'feature',
      expectedHeadRepoFullName: 'owner/repo',
    }, run),
    true,
  );
  assert.equal(
    shouldProcessPullRequest(latest, {
      expectedHeadSha: 'branch-head',
      expectedHeadRef: 'different',
      expectedHeadRepoFullName: 'owner/repo',
    }, run),
    false,
  );
  assert.equal(
    shouldProcessPullRequest(latest, {
      expectedHeadSha: 'branch-head',
      expectedHeadRef: 'feature',
      expectedHeadRepoFullName: 'other/repo',
    }, run),
    false,
  );
});
