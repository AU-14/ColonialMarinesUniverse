const fs = require('node:fs');

const labels = {
  requiredPassed: 'S: Required Checks Passed',
  requiredFailed: 'S: Required Checks Failed',
  integrationPassed: 'S: Integration Tests Passed',
  integrationFailed: 'S: Integration Tests Failed',
};

const labelDefinitions = {
  [labels.requiredPassed]: {
    color: '0e8a16',
    description: 'Required CI checks passed for the current pull request head.',
  },
  [labels.requiredFailed]: {
    color: 'd93f0b',
    description: 'Required CI checks failed for the current pull request head.',
  },
  [labels.integrationPassed]: {
    color: '0e8a16',
    description: 'Integration tests passed for the current pull request head.',
  },
  [labels.integrationFailed]: {
    color: 'd93f0b',
    description: 'Integration tests failed for the current pull request head.',
  },
};

function dedupePullRequestTargets(targets) {
  const byNumber = new Map();

  for (const target of targets) {
    const existing = byNumber.get(target.number);
    if (!existing || (!existing.expectedHeadSha && target.expectedHeadSha)) {
      byNumber.set(target.number, target);
    }
  }

  return Array.from(byNumber.values());
}

function normalizePullRequestTargets(pullRequests, source) {
  const targets = [];

  for (const pullRequest of pullRequests || []) {
    const number = Number(pullRequest.number ?? pullRequest.pull_number);
    if (!Number.isInteger(number) || number <= 0) {
      continue;
    }

    targets.push({
      number,
      expectedHeadSha: pullRequest.head_sha || pullRequest.headSha || pullRequest.head?.sha || null,
      expectedHeadRef: pullRequest.head_ref || pullRequest.headRef || pullRequest.head?.ref || null,
      expectedHeadRepoFullName: pullRequest.head_repo_full_name || pullRequest.headRepoFullName || pullRequest.head?.repo?.full_name || null,
      source,
    });
  }

  return dedupePullRequestTargets(targets);
}

function parsePullRequestMetadata(contents) {
  const parsed = JSON.parse(contents);
  const pullRequests = Array.isArray(parsed) ? parsed : [parsed];
  return normalizePullRequestTargets(pullRequests, 'artifact');
}

function readPullRequestMetadata(metadataPath) {
  if (!fs.existsSync(metadataPath)) {
    return [];
  }

  return parsePullRequestMetadata(fs.readFileSync(metadataPath, 'utf8'));
}

function shouldProcessPullRequestHead(latestHeadSha, expectedHeadSha) {
  return !expectedHeadSha || latestHeadSha === expectedHeadSha;
}

function shouldProcessPullRequest(latest, target, run) {
  if (!shouldProcessPullRequestHead(latest.head.sha, target.expectedHeadSha)) {
    return false;
  }

  const runHeadRepo = run.head_repository?.full_name;
  if (runHeadRepo && latest.head.repo?.full_name !== runHeadRepo) {
    return false;
  }

  if (run.head_branch && latest.head.ref !== run.head_branch) {
    return false;
  }

  if (target.expectedHeadRepoFullName && latest.head.repo?.full_name !== target.expectedHeadRepoFullName) {
    return false;
  }

  if (target.expectedHeadRef && latest.head.ref !== target.expectedHeadRef) {
    return false;
  }

  return true;
}

async function ensureLabels(github, owner, repo) {
  const existing = new Map((await github.paginate(github.rest.issues.listLabelsForRepo, {
    owner,
    repo,
    per_page: 100,
  })).map(label => [label.name, label]));

  for (const [name, config] of Object.entries(labelDefinitions)) {
    const current = existing.get(name);
    if (!current) {
      await github.rest.issues.createLabel({ owner, repo, name, ...config });
      continue;
    }

    if (current.color.toLowerCase() !== config.color || (current.description || '') !== config.description) {
      await github.rest.issues.updateLabel({ owner, repo, name, new_name: name, ...config });
    }
  }
}

async function getPullRequestTargets(github, owner, repo, run, core, metadataPath) {
  const artifactTargets = readPullRequestMetadata(metadataPath);
  if (artifactTargets.length > 0) {
    core.info(`Using PR metadata artifact for CI run ${run.id}.`);
    return artifactTargets;
  }

  const workflowRunTargets = normalizePullRequestTargets(run.pull_requests, 'workflow_run');
  if (workflowRunTargets.length > 0) {
    core.info(`Using workflow_run pull request payload for CI run ${run.id}.`);
    return workflowRunTargets;
  }

  const response = await github.rest.repos.listPullRequestsAssociatedWithCommit({
    owner,
    repo,
    commit_sha: run.head_sha,
  });
  const associatedTargets = normalizePullRequestTargets(response.data, 'commit_association');
  if (associatedTargets.length > 0) {
    core.info(`Using commit-associated pull requests for CI run ${run.id}.`);
  }

  return associatedTargets;
}

async function setExclusiveLabels(github, owner, repo, issueNumber, addName, removeNames) {
  const current = await github.paginate(github.rest.issues.listLabelsOnIssue, {
    owner,
    repo,
    issue_number: issueNumber,
    per_page: 100,
  });
  const currentNames = new Set(current.map(label => label.name));

  for (const name of removeNames) {
    if (currentNames.has(name)) {
      await github.rest.issues.removeLabel({ owner, repo, issue_number: issueNumber, name });
    }
  }

  if (!currentNames.has(addName)) {
    await github.rest.issues.addLabels({ owner, repo, issue_number: issueNumber, labels: [addName] });
  }
}

async function applyCiStatusLabels({ github, context, core, metadataPath = 'ci-pr-metadata/pr.json' }) {
  const owner = context.repo.owner;
  const repo = context.repo.repo;
  const run = context.payload.workflow_run;

  const pullRequests = await getPullRequestTargets(github, owner, repo, run, core, metadataPath);
  if (pullRequests.length === 0) {
    core.info(`No pull request found for CI run ${run.id}.`);
    return;
  }

  const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRun, {
    owner,
    repo,
    run_id: run.id,
    per_page: 100,
  });

  const requiredJob = jobs.find(job => job.name === 'Required Checks');
  const integrationJobs = jobs.filter(job => job.name.startsWith('Testcases / '));

  await ensureLabels(github, owner, repo);

  for (const pullRequest of pullRequests) {
    const pullNumber = pullRequest.number;
    const { data: latest } = await github.rest.pulls.get({
      owner,
      repo,
      pull_number: pullNumber,
    });

    if (!shouldProcessPullRequest(latest, pullRequest, run)) {
      core.info(`Skipping PR #${pullNumber}: PR head no longer matches CI run ${run.id}.`);
      continue;
    }

    if (requiredJob) {
      const requiredPassed = requiredJob.conclusion === 'success';
      await setExclusiveLabels(
        github,
        owner,
        repo,
        pullNumber,
        requiredPassed ? labels.requiredPassed : labels.requiredFailed,
        requiredPassed ? [labels.requiredFailed] : [labels.requiredPassed],
      );
    } else {
      core.warning(`Required Checks job was not found in CI run ${run.id}.`);
    }

    if (integrationJobs.length > 0) {
      const integrationPassed = integrationJobs.every(job => job.conclusion === 'success');
      await setExclusiveLabels(
        github,
        owner,
        repo,
        pullNumber,
        integrationPassed ? labels.integrationPassed : labels.integrationFailed,
        integrationPassed ? [labels.integrationFailed] : [labels.integrationPassed],
      );
    }
  }
}

module.exports = {
  applyCiStatusLabels,
  labels,
  labelDefinitions,
  normalizePullRequestTargets,
  parsePullRequestMetadata,
  shouldProcessPullRequest,
  shouldProcessPullRequestHead,
};
