// CMU14: stacking test merges. "Test Merge" is the wanted state: add the label after a review, or type
// the number into the dispatch and the bot adds it. The "TM: *" labels are bot status and only change
// after a publish actually succeeded.
const REQUEST = 'Test Merge';
const LIVE = 'TM: Live';
const CONFLICT = 'TM: Conflict';
const OUTDATED = 'TM: Outdated';
const STATUS = [LIVE, CONFLICT, OUTDATED];
const ALL = [REQUEST, ...STATUS];
const MARKER = '<!-- cmu-test-merge-status -->';
const ALLOWED_BASES = new Set(['master', 'staging', 'stable']);

const LABEL_STYLE = {
  [REQUEST]: { color: 'fbca04', description: 'This PR should be on the server as a test merge' },
  [LIVE]: { color: '0e8a16', description: 'Bot: this PR is in the deployed test merge build' },
  [CONFLICT]: { color: 'b60205', description: 'Bot: skipped from the last test merge because it conflicted' },
  [OUTDATED]: { color: 'd93f0b', description: 'Bot: PR got new commits after it was test merged' },
};

const shortSha = sha => sha.substring(0, 7);

function formatUtc(date) {
  const pad = value => value.toString().padStart(2, '0');
  return [
    `${pad(date.getUTCMonth() + 1)}/${pad(date.getUTCDate())}/${date.getUTCFullYear()}`,
    `${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())}:${pad(date.getUTCSeconds())} +00:00`,
  ].join(' ');
}

function helpers({ github, context, core }) {
  const repo = context.repo;
  const commitLink = sha => `${context.serverUrl}/${repo.owner}/${repo.repo}/commit/${sha}`;

  async function ensureLabels() {
    for (const name of ALL) {
      try {
        await github.rest.issues.getLabel({ ...repo, name });
      } catch (error) {
        if (error.status !== 404) throw error;
        await github.rest.issues.createLabel({ ...repo, name, ...LABEL_STYLE[name] });
      }
    }
  }

  async function labelsOf(number) {
    const { data } = await github.rest.issues.listLabelsOnIssue({ ...repo, issue_number: number, per_page: 100 });
    return new Set(data.map(label => label.name));
  }

  async function setLabels(number, { add = [], remove = [] }) {
    const current = await labelsOf(number);
    const toAdd = add.filter(name => !current.has(name));
    if (toAdd.length > 0) {
      await github.rest.issues.addLabels({ ...repo, issue_number: number, labels: toAdd });
    }
    for (const name of remove) {
      if (!current.has(name)) continue;
      try {
        await github.rest.issues.removeLabel({ ...repo, issue_number: number, name });
      } catch (error) {
        if (error.status !== 404) throw error;
      }
    }
    return current;
  }

  // One status comment per PR, edited in place on every deploy instead of piling up.
  async function upsertComment(number, text) {
    const body = `${MARKER}\n${text}`;
    try {
      const comments = await github.paginate(github.rest.issues.listComments, { ...repo, issue_number: number, per_page: 100 });
      const existing = comments.find(comment => comment.user?.type === 'Bot' && comment.body?.includes(MARKER));
      if (existing) {
        await github.rest.issues.updateComment({ ...repo, comment_id: existing.id, body });
      } else {
        await github.rest.issues.createComment({ ...repo, issue_number: number, body });
      }
    } catch (error) {
      if (error.status === 403) {
        core.warning(`Could not write the test merge comment on PR #${number}: ${error.message}`);
        return;
      }
      throw error;
    }
  }

  async function openPullsWithAny(names) {
    const found = new Map();
    for (const name of names) {
      const issues = await github.paginate(github.rest.issues.listForRepo, { ...repo, state: 'open', labels: name, per_page: 100 });
      for (const issue of issues) {
        if (issue.pull_request) found.set(issue.number, issue);
      }
    }
    return [...found.values()].sort((a, b) => a.number - b.number);
  }

  return { repo, commitLink, ensureLabels, labelsOf, setLabels, upsertComment, openPullsWithAny };
}

// Resolves the PR set: every open PR labelled "Test Merge", plus the numbers typed into the dispatch,
// minus the ones typed into the remove box.
async function collect({ github, context, core }) {
  const { repo, openPullsWithAny } = helpers({ github, context, core });
  const parse = raw => [...new Set(((raw ?? '').match(/\d+/g) ?? []).map(Number))]
    .filter(number => Number.isInteger(number) && number > 0);
  const explicit = parse(process.env.PR_NUMBERS);
  const removed = new Set(parse(process.env.REMOVE_NUMBERS));
  const allowEmpty = process.env.ALLOW_EMPTY === 'true';

  const both = explicit.filter(number => removed.has(number));
  if (both.length > 0) {
    core.setFailed(`${both.map(number => `#${number}`).join(', ')} is in both the add and the remove box.`);
    return;
  }

  const explicitPulls = [];
  for (const number of explicit) {
    const { data: pull } = await github.rest.pulls.get({ ...repo, pull_number: number });
    if (pull.state !== 'open') {
      core.setFailed(`PR #${number} is ${pull.state}; only open PRs can be test merged.`);
      return;
    }
    if (!ALLOWED_BASES.has(pull.base.ref)) {
      core.setFailed(`PR #${number} targets ${pull.base.ref}; test merges only support master, staging, or stable.`);
      return;
    }
    explicitPulls.push(pull);
  }

  const remembered = [];
  for (const issue of await openPullsWithAny([REQUEST])) {
    if (removed.has(issue.number) || explicit.includes(issue.number)) continue;
    const { data: pull } = await github.rest.pulls.get({ ...repo, pull_number: issue.number });
    remembered.push(pull);
  }

  const baseRef = explicitPulls[0]?.base.ref ?? remembered[0]?.base.ref ?? 'master';
  const mismatched = explicitPulls.filter(pull => pull.base.ref !== baseRef);
  if (mismatched.length > 0) {
    core.setFailed(`All PRs must target the same base branch. Got: ${explicitPulls.map(pull => `#${pull.number}->${pull.base.ref}`).join(', ')}`);
    return;
  }

  const pulls = new Map(explicitPulls.map(pull => [pull.number, pull]));
  for (const pull of remembered) {
    if (pull.base.ref !== baseRef) {
      core.warning(`Dropping PR #${pull.number} from the test merge: it targets ${pull.base.ref}, this run is onto ${baseRef}.`);
      continue;
    }
    pulls.set(pull.number, pull);
  }

  const ordered = [...pulls.values()]
    .sort((a, b) => a.number - b.number)
    .map(pull => ({ number: pull.number, title: pull.title, url: pull.html_url, head_sha: pull.head.sha }));

  if (ordered.length === 0 && !allowEmpty) {
    core.setFailed(`Nothing left to test merge: label PRs "${REQUEST}" or type their numbers, or run Publish for a clean master.`);
    return;
  }

  const { data: baseBranch } = await github.rest.git.getRef({ ...repo, ref: `heads/${baseRef}` });
  core.setOutput('base_ref', baseRef);
  core.setOutput('base_sha', baseBranch.object.sha);
  core.setOutput('prs_json', JSON.stringify(ordered));
  core.setOutput('removed_json', JSON.stringify([...removed]));
  core.notice(ordered.length > 0
    ? `Test merging ${ordered.map(pull => `#${pull.number}`).join(', ')} onto ${baseRef} @ ${baseBranch.object.sha}.`
    : `No test merge PRs; publishing ${baseRef} @ ${baseBranch.object.sha}.`);
}

// After a successful test merge publish: merged PRs go live, conflicting ones are flagged,
// and anything that was live before but is not in this build loses its status.
async function finalize({ github, context, core }) {
  const h = helpers({ github, context, core });
  const baseSha = process.env.BASE_SHA;
  const mergeSha = process.env.MERGE_SHA;
  const merged = JSON.parse(process.env.MERGED_JSON || '[]');
  const conflicts = JSON.parse(process.env.CONFLICTS_JSON || '[]');
  const removed = JSON.parse(process.env.REMOVED_JSON || '[]');
  const at = formatUtc(new Date());

  await h.ensureLabels();

  for (const number of removed) {
    await h.setLabels(number, { remove: [REQUEST] });
  }

  const pullLines = merged.map(pull =>
    `PR #${pull.number}: [${shortSha(pull.head_sha)}](${h.commitLink(pull.head_sha)}) ${pull.title}`);
  const liveBody = [
    '<details>',
    `<summary>Test Merge Deployed @ ${at}:</summary>`,
    '',
    '**Revision**',
    '',
    `Origin: [${shortSha(baseSha)}](${h.commitLink(baseSha)})`,
    ...pullLines,
    `Server: ${mergeSha}`,
    '</details>',
  ].join('\n');

  for (const pull of merged) {
    const { data: latest } = await github.rest.pulls.get({ ...h.repo, pull_number: pull.number });
    const outdated = latest.head.sha !== pull.head_sha;
    await h.setLabels(pull.number, {
      add: outdated ? [REQUEST, LIVE, OUTDATED] : [REQUEST, LIVE],
      remove: outdated ? [CONFLICT] : [CONFLICT, OUTDATED],
    });
    await h.upsertComment(pull.number, liveBody);
  }

  for (const pull of conflicts) {
    await h.setLabels(pull.number, { add: [REQUEST, CONFLICT], remove: [LIVE, OUTDATED] });
    const files = pull.files.length > 0 ? pull.files.map(file => `- \`${file}\``).join('\n') : '- (git did not list the files)';
    const before = merged.filter(other => other.number < pull.number).map(other => `#${other.number}`);
    await h.upsertComment(pull.number, [
      `**Test merge skipped @ ${at}:** this PR conflicts with \`${shortSha(baseSha)}\`${before.length > 0 ? ` plus the earlier test merges (${before.join(', ')})` : ''}.`,
      '',
      files,
      '',
      `It keeps the \`${REQUEST}\` label: resolve the conflict and the next run picks it up again, or remove the label to drop it.`,
    ].join('\n'));
  }

  const inBuild = new Set([...merged, ...conflicts].map(pull => pull.number));
  for (const issue of await h.openPullsWithAny(STATUS)) {
    if (inBuild.has(issue.number)) continue;
    const had = await h.setLabels(issue.number, { remove: STATUS });
    if (had.has(LIVE) || had.has(OUTDATED)) {
      await h.upsertComment(issue.number, `**Test merge ended @ ${at}:** removed, not part of the new test merge build \`${shortSha(mergeSha)}\`.`);
    }
  }
}

// After a successful clean (non test merge) publish: every test merge is gone from the server.
async function endAll({ github, context, core }) {
  const h = helpers({ github, context, core });
  const version = process.env.PUBLISH_VERSION;
  const at = formatUtc(new Date());

  for (const issue of await h.openPullsWithAny(ALL)) {
    const had = await h.setLabels(issue.number, { remove: ALL });
    if (had.has(LIVE) || had.has(OUTDATED)) {
      await h.upsertComment(issue.number, `**Test merge ended @ ${at}:** removed from the server by publish [${shortSha(version)}](${h.commitLink(version)}). Add the \`${REQUEST}\` label or type the PR number into Publish Test Merge to test it again.`);
    }
  }
}

// pull_request_target: new commits mark a live test merge outdated; closing clears every TM label.
async function onPullRequest({ github, context, core }) {
  const h = helpers({ github, context, core });
  const pr = context.payload.pull_request;
  const at = formatUtc(new Date());

  if (context.payload.action === 'synchronize') {
    const current = await h.labelsOf(pr.number);
    if (current.has(LIVE)) {
      await h.ensureLabels();
      await h.setLabels(pr.number, { add: [OUTDATED] });
    }
    return;
  }

  if (context.payload.action === 'closed') {
    const had = await h.setLabels(pr.number, { remove: ALL });
    if (!had.has(LIVE) && !had.has(OUTDATED)) return;
    await h.upsertComment(pr.number, pr.merged
      ? `**Test merge ended @ ${at}:** PR merged. The test merge build stays on the server until the next publish, which ships it from \`${pr.base.ref}\`.`
      : `**Test merge ended @ ${at}:** PR closed. It stays on the server until the next publish or test merge run.`);
  }
}

module.exports = { collect, finalize, endAll, onPullRequest };
