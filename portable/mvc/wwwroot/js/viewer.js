const emptyState = document.querySelector('#viewer-empty');
const startButton = document.querySelector('#start-browser');
const errorText = document.querySelector('#start-error');
let csrfToken;
let browserSessionId;
let currentSession;
let pollTimer;
let opening = false;
let activePipelineStep = 0;
let lastPolledStatus;
const renderedProgress = new Set();
const pipelineNodes = [...document.querySelectorAll('.pipeline-node')];
const pipelineLog = document.querySelector('#pipeline-log');
const pipelineSummary = document.querySelector('#pipeline-summary');

function setPipelineStep(step, summary) {
  activePipelineStep = step;
  for (const [index, node] of pipelineNodes.entries()) {
    node.className = `pipeline-node ${index < step ? 'done' : index === step ? 'active' : 'pending'}`;
  }
  if (summary) pipelineSummary.textContent = summary;
  pipelineNodes[step]?.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
}

function failPipeline(message) {
  const node = pipelineNodes[activePipelineStep];
  if (node) node.className = 'pipeline-node error';
  pipelineSummary.textContent = 'เกิดข้อผิดพลาด';
  logActivity('ERROR', message, true);
}

function logActivity(source, message, isError = false) {
  const row = document.createElement('div');
  row.className = `pipeline-event${isError ? ' error' : ''}`;
  const time = document.createElement('time');
  time.textContent = new Date().toLocaleTimeString('th-TH', { hour12: false });
  const label = document.createElement('b');
  label.textContent = source;
  const detail = document.createElement('span');
  detail.textContent = message;
  row.append(time, label, detail);
  pipelineLog.append(row);
  pipelineLog.scrollTop = pipelineLog.scrollHeight;
}

function pipelineStepForAgentStatus(status) {
  if (status === 'starting') return 3;
  if (status === 'launching-edge') return 4;
  if (['navigating-google', 'entering-email', 'entering-password', 'waiting-google', 'session-reused', 'returning', 'ready'].includes(status)) {
    return status === 'ready' ? 6 : 5;
  }
  if (status === 'handing-off') return 6;
  if (status === 'handed-off') return 7;
  return activePipelineStep;
}

function renderAgentProgress(progress = []) {
  for (const event of progress) {
    const key = `${event.at}:${event.status}`;
    if (renderedProgress.has(key)) continue;
    renderedProgress.add(key);
    setPipelineStep(pipelineStepForAgentStatus(event.status), event.message);
    logActivity('AGENT', `${event.status} — ${event.message}`, event.status === 'error');
  }
}

async function requestJson(url, options = {}) {
  const response = await fetch(url, {
    credentials: 'same-origin',
    ...options,
    headers: {
      Accept: 'application/json',
      ...(options.method && options.method !== 'GET' ? { 'X-CSRF-Token': csrfToken } : {}),
      ...(options.headers ?? {})
    }
  });
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(body.error ?? body.title ?? `Request failed (${response.status})`);
  return body;
}

function setWorkerStatus(status, message) {
  document.querySelector('#worker-status').textContent = message ?? status;
  document.querySelector('#worker-dot').className = status;
}

async function loadIdentity() {
  setPipelineStep(1, 'กำลังตรวจ SSO identity');
  logActivity('SSO', 'POST /sso/login → 302 /viewer และออก HttpOnly Broker cookie');
  logActivity('API', 'GET /api/v1/me — ขอ SSO owner และ account mapping');
  const me = await requestJson('/api/v1/me');
  csrfToken = me.csrfToken;
  document.querySelector('#mapped-account').textContent = me.account.email;
  logActivity('API', `GET /api/v1/me → 200; mapped account=${me.account.email}`);
  setPipelineStep(2, 'ยืนยัน identity และ mapping แล้ว');
}

async function ensureBrowserSession() {
  startButton.disabled = true;
  setPipelineStep(2, 'กำลังสร้าง opaque Broker session');
  logActivity('API', 'POST /api/v1/browser-sessions — ส่ง X-CSRF-Token เพื่อสร้าง session');
  try {
    const result = await requestJson('/api/v1/browser-sessions', { method: 'POST' });
    browserSessionId = result.sessionId;
    logActivity('API', `POST /api/v1/browser-sessions → 202; sessionId=${result.sessionId}`);
    setPipelineStep(3, 'Broker ส่งคำสั่ง start ไป Local Agent');
    setWorkerStatus(result.status, 'กำลังเตรียม Secure Browser...');
    pollStatus();
  } catch (error) {
    startButton.disabled = false;
    errorText.textContent = error.message;
    failPipeline(error.message);
  }
}

function beginBrowserFlow() {
  startButton.disabled = true;
  if (currentSession && !['not-started', 'error'].includes(currentSession.status)) {
    renderAgentProgress(currentSession.progress);
    setWorkerStatus(currentSession.status, currentSession.message);
    pollStatus();
    return;
  }

  currentSession = null;
  ensureBrowserSession();
}

async function pollStatus() {
  clearTimeout(pollTimer);
  if (!browserSessionId) return;
  try {
    const result = await requestJson(`/api/v1/browser-sessions/${encodeURIComponent(browserSessionId)}`);
    renderAgentProgress(result.progress);
    setWorkerStatus(result.status, result.message);
    if (result.status !== lastPolledStatus) {
      logActivity('API', `GET /api/v1/browser-sessions/{id} → 200; status=${result.status}`);
      lastPolledStatus = result.status;
    }
    if (result.status === 'ready') {
      if (!opening) openRealBrowser();
      return;
    }
    if (result.status === 'handed-off') {
      showComplete();
      return;
    }
    if (result.status === 'error') {
      errorText.textContent = result.message;
      startButton.disabled = false;
      failPipeline(result.message);
      return;
    }
  } catch (error) {
    errorText.textContent = error.message;
    failPipeline(error.message);
  }
  pollTimer = setTimeout(pollStatus, 700);
}

async function openRealBrowser() {
  opening = true;
  setPipelineStep(6, 'Google session พร้อม กำลัง handoff ไป Edge หน้าจริง');
  logActivity('API', 'POST /api/v1/browser-sessions/{id}/open — ขอ Browser handoff');
  setWorkerStatus('handing-off', 'กำลังเปิด Microsoft Edge หน้าจริง...');
  try {
    const result = await requestJson(
      `/api/v1/browser-sessions/${encodeURIComponent(browserSessionId)}/open`,
      { method: 'POST' }
    );
    renderAgentProgress(result.progress);
    logActivity('API', `POST /api/v1/browser-sessions/{id}/open → 200; status=${result.status}`);
    if (result.status === 'handed-off') showComplete();
  } catch (error) {
    opening = false;
    errorText.textContent = error.message;
    failPipeline(error.message);
  }
}

function showComplete() {
  setPipelineStep(7, 'Success — Login สำเร็จ');
  logActivity('SUCCESS', 'Google session พร้อมและเปิด Microsoft Edge หน้าจริงแล้ว');
  emptyState.querySelector('h1').textContent = 'เปิด Gemini หน้าจริงแล้ว';
  emptyState.querySelector('p').textContent = 'Login สำเร็จ';
  startButton.hidden = true;
  errorText.textContent = '';
  setWorkerStatus('ready', 'Browser handoff สำเร็จ');
}

async function endSession() {
  clearTimeout(pollTimer);
  if (browserSessionId) {
    logActivity('API', 'DELETE /api/v1/browser-sessions/{id} — สิ้นสุด session');
    await requestJson(`/api/v1/browser-sessions/${encodeURIComponent(browserSessionId)}`, { method: 'DELETE' });
  }
  window.location.href = '/viewer?stopped=1';
}

startButton.addEventListener('click', beginBrowserFlow);
document.querySelector('#end-session').addEventListener('click', endSession);

loadIdentity()
  .then(async () => {
    logActivity('API', 'GET /api/v1/browser-sessions/current — ตรวจ session ที่มีอยู่');
    const current = await requestJson('/api/v1/browser-sessions/current').catch(() => null);
    if (current) {
      browserSessionId = current.sessionId;
      currentSession = current;
      logActivity('API', `GET /api/v1/browser-sessions/current → 200; status=${current.status}`);
      setWorkerStatus(current.status, `${current.message ?? current.status} — รอผู้ใช้กด เปิด Gemini Chat`);
      setPipelineStep(2, 'พบ Broker session เดิม; รอผู้ใช้เริ่ม flow');
      startButton.disabled = false;
      return;
    }

    logActivity('API', 'GET /api/v1/browser-sessions/current → 404; รอผู้ใช้เริ่ม flow');
    setWorkerStatus('not-started', 'รอผู้ใช้กด เปิด Gemini Chat');
    setPipelineStep(2, 'พร้อมเริ่ม Broker session');
    startButton.disabled = false;
  })
  .catch((error) => {
    errorText.textContent = error.message;
    failPipeline(error.message);
  });

