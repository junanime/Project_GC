// Deploy as QaRun. Parameters: action (String, required), payload (String, required).
// QA sanity checks, not a simulation-authoritative anti-cheat system.
const { DataApi } = require('@unity-services/cloud-save-1.4');
const { LeaderboardsApi } = require('@unity-services/leaderboards-1.1');
const BOARD = 'qa_s1_v1';
const KEY = 'qa_run_v1';
const weights = { normal: 30, special: 50, elite: 100, miniBoss: 500, boss: 1500, entered: 100, cleared: 300, overcharge: 200 };
function validate(data, elapsed) {
  if (data.rules !== BOARD || typeof data.success !== 'boolean') throw Error('RULES_MISMATCH');
  if (!Number.isFinite(data.seconds) || data.seconds < 0 || data.seconds > elapsed + 15 || elapsed > 86400) throw Error('INVALID_DURATION');
  // Two fields each place 14 unique clots; stage transfer preserves the same run.
  const caps = { normal: Math.min(200000, Math.ceil(data.seconds * 120 + 100)), special: 2000, elite: 500, miniBoss: 20, boss: 2, entered: 28, cleared: 28, overcharge: 28 };
  let score = data.success ? 1000 : 0;
  for (const [key, weight] of Object.entries(weights)) {
    if (!Number.isInteger(data[key]) || data[key] < 0 || data[key] > caps[key]) throw Error('INVALID_COUNTER_' + key);
    score += data[key] * weight;
  }
  if (data.cleared > data.entered || (data.success && (data.boss < 1 || data.seconds < 60))) throw Error('INVALID_PROGRESS');
  return score;
}
module.exports = async ({ params, context }) => {
  if (!context.playerId) throw Error('PLAYER_REQUIRED');
  const data = JSON.parse(params.payload);
  if (typeof data.runId !== 'string' || !/^[a-f0-9]{32}$/.test(data.runId)) throw Error('INVALID_RUN_ID');
  const save = new DataApi(context);
  const api = new LeaderboardsApi(context);
  const read = await save.getProtectedItems(context.projectId, context.playerId, [KEY]);
  const item = read.data.results.find(x => x.key === KEY);
  let run = item && item.value;
  const write = async value => save.setProtectedItem(context.projectId, context.playerId,
    { key: KEY, value, ...(item ? { writeLock: item.writeLock } : {}) });
  if (params.action === 'begin' || params.action === 'begin-test') {
    if (run && run.runId === data.runId) return { runId: run.runId, score: run.score || 0 };
    // Recover any score whose final service write failed before starting another run.
    if (run && run.final && !run.rejected) await api.addLeaderboardPlayerScore(context.projectId, run.board || BOARD, context.playerId, { score: run.score });
    run = { runId: data.runId, started: Date.now(), final: false, board: params.action === 'begin-test' ? 'Validation_Only' : BOARD };
    await write(run);
    return { runId: run.runId, score: 0 };
  }
  if (params.action !== 'submit') throw Error('INVALID_ACTION');
  // A stale/off-device ticket cannot become valid on retry; acknowledge rejection so clients can start a new run.
  if (!run || run.runId !== data.runId) return { runId: data.runId, score: 0, rejected: 'RUN_NOT_FOUND' };
  if (!run.final) {
    try { run.score = validate(data, (Date.now() - run.started) / 1000); }
    catch (e) { run.rejected = e.message; }
    run.final = true;
    run.summary = data;
    // Finalize before score write: retries cannot replace the accepted counters.
    await write(run);
  }
  if (run.rejected) return { runId: run.runId, score: 0, rejected: run.rejected };
  await api.addLeaderboardPlayerScore(context.projectId, run.board || BOARD, context.playerId, { score: run.score });
  return { runId: run.runId, score: run.score };
};
module.exports.params = { action: { type: 'String', required: true }, payload: { type: 'String', required: true } };
module.exports.validate = validate;
