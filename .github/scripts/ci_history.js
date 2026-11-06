// CMU14: bounded, read-only history lookup. Artifacts are data, never executable code.
module.exports = async ({ github, context, core }) => {
  const fs = require('node:fs');
  const repo = context.repo;
  const repository = `${repo.owner}/${repo.repo}`;
  const defaultBranch = context.payload.repository.default_branch;
  const provenance = { repository, default_branch: defaultBranch };
  const pr = context.payload.pull_request;
  const branch = pr?.head.ref ?? context.ref.replace('refs/heads/', '');
  const headRepository = pr?.head.repo.full_name ?? repository;
  const event = pr ? 'pull_request' : context.eventName;
  const artifact = async run => {
    const response = await github.rest.actions.listWorkflowRunArtifacts({ ...repo, run_id: run.id, per_page: 100 });
    return response.data.artifacts.find(a => a.name === 'ci-history' && !a.expired);
  };
  const remember = (kind, run, found) => {
    provenance[kind] = {
      id: run.id, head_sha: run.head_sha, event: run.event, conclusion: run.conclusion,
      head_branch: run.head_branch, head_repository: run.head_repository?.full_name,
      html_url: run.html_url,
    };
    core.setOutput(`${kind}_run`, String(run.id));
    core.setOutput(`${kind}_artifact`, String(found.id));
  };
  try {
    const recent = await github.rest.actions.listWorkflowRuns({
      ...repo, workflow_id: 'ci.yml', branch, event, status: 'completed', per_page: 20,
    });
    for (const run of recent.data.workflow_runs) {
      if (run.id >= context.runId || run.head_repository?.full_name !== headRepository) continue;
      if (provenance.latest && (provenance.baseline || run.conclusion !== 'success')) continue;
      const found = await artifact(run);
      if (!found) continue;
      if (!provenance.latest) remember('latest', run, found);
      if (!provenance.baseline && run.conclusion === 'success') remember('baseline', run, found);
      if (provenance.latest && provenance.baseline) break;
    }
    const green = await github.rest.actions.listWorkflowRuns({
      ...repo, workflow_id: 'ci.yml', branch: defaultBranch, event: 'push', status: 'success', per_page: 10,
    });
    for (const run of green.data.workflow_runs) {
      if (run.id >= context.runId || run.head_repository?.full_name !== repository) continue;
      const found = await artifact(run);
      if (found) { remember('trusted', run, found); break; }
    }
  } catch (error) {
    core.warning(`History unavailable; ordinary full-coverage execution remains available: ${error.message}`);
  }
  fs.mkdirSync('.ci', { recursive: true });
  fs.writeFileSync('.ci/provenance.json', JSON.stringify(provenance));
};
