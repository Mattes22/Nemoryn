const state = {
  ownerId: localStorage.getItem("memory.ownerId") || "",
  conversations: [],
  conversationId: localStorage.getItem("memory.conversationId") || "",
  messages: [],
  memories: [],
  candidates: [],
  conflicts: [],
  ingestionJobs: [],
  auditLogs: [],
  toolAuditLogs: [],
  cleanupPreview: null,
  prepared: null,
  lastAgentResult: null,
  agentBusy: false,
  evalScenarios: localStorage.getItem("memory.evalScenarios") || "",
  evalResult: null,
  evalBusy: false,
  policy: null,
  backupExport: null,
  backupDocument: null,
  backupFileName: "",
  backupPreview: null,
  backupBusy: false,
  backupSkipDuplicates: true,
  backupPinExplicit: false,
  owners: [],
  runtime: null,
  runtimeBusy: false,
  aiConnection: null,
  aiConnectionDraft: null,
  aiConnectionNotice: "",
  databaseConnection: null,
  databaseConnectionDraft: null,
  databaseConnectionNotice: "",
  tools: [],
  permissionProfiles: [],
  permissionProfile: "Safe",
  tab: "memories",
  pollTimer: 0,
};

const els = {
  ownerForm: document.querySelector("#owner-form"),
  ownerPick: document.querySelector("#owner-pick"),
  ownerId: document.querySelector("#owner-id"),
  ownerOptions: document.querySelector("#owner-options"),
  health: document.querySelector("#health"),
  policyChip: document.querySelector("#policy-chip"),
  banner: document.querySelector("#banner"),
  conversationList: document.querySelector("#conversation-list"),
  newConversation: document.querySelector("#new-conversation"),
  newConversationForm: document.querySelector("#new-conversation-form"),
  cancelNewConversation: document.querySelector("#cancel-new-conversation"),
  newExternalId: document.querySelector("#new-external-id"),
  newTitle: document.querySelector("#new-title"),
  chatTitle: document.querySelector("#chat-title"),
  chatMeta: document.querySelector("#chat-meta"),
  messages: document.querySelector("#messages"),
  composer: document.querySelector("#composer"),
  messageRole: document.querySelector("#message-role"),
  messageContent: document.querySelector("#message-content"),
  panelMemories: document.querySelector("#panel-memories"),
  panelCandidates: document.querySelector("#panel-candidates"),
  panelConflicts: document.querySelector("#panel-conflicts"),
  panelIngestion: document.querySelector("#panel-ingestion"),
  panelAudit: document.querySelector("#panel-audit"),
  panelCleanup: document.querySelector("#panel-cleanup"),
  panelEval: document.querySelector("#panel-eval"),
  panelPolicy: document.querySelector("#panel-policy"),
  panelBackup: document.querySelector("#panel-backup"),
  panelAgent: document.querySelector("#panel-agent"),
  panelRuntime: document.querySelector("#panel-runtime"),
  localeSelect: document.querySelector("#locale-select"),
};

const defaultEvalScenarios = `{
  "limit": 8,
  "cases": [
    { "id": "name", "query": "Jak se jmenuju?", "expectedContentContains": ["Matej"] },
    { "id": "home", "query": "Kde bydlím?", "expectedContentContains": ["Brno"] },
    { "id": "style", "query": "Jak mám rád odpovědi?", "expectedContentContains": ["Czech"] }
  ]
}`;

function showError(error) {
  els.banner.hidden = !error;
  els.banner.textContent = error || "";
}

async function api(path, options = {}) {
  const { timeoutMs = 60000, headers, ...fetchOptions } = options;
  let response;
  try {
    response = await fetch(path, {
      headers: { "Content-Type": "application/json", ...(headers || {}) },
      ...fetchOptions,
      signal: AbortSignal.timeout(timeoutMs),
    });
  } catch (error) {
    if (error.name === "TimeoutError" || error.name === "AbortError") {
      throw new Error(t("error.apiTimeout"));
    }

    throw error;
  }

  if (response.status === 204) {
    return null;
  }

  const text = await response.text();
  let body = null;
  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = null;
    }
  }

  if (!response.ok) {
    const detail = body?.detail || body?.title || text || response.statusText;
    throw new Error(detail);
  }

  return body;
}

async function loadToolAudit(path) {
  try {
    const entries = await api(path, { timeoutMs: 8000 });
    return Array.isArray(entries) ? entries : [];
  } catch {
    return [];
  }
}

function selectedConversation() {
  return state.conversations.find((item) => item.id === state.conversationId) || null;
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function emptyState(title, detail) {
  return `
    <div class="empty-state">
      <strong>${escapeHtml(title)}</strong>
      <p>${escapeHtml(detail)}</p>
    </div>
  `;
}

function noticeText(notice) {
  if (!notice) {
    return "";
  }

  if (typeof notice === "string") {
    return t(notice);
  }

  return t(notice.key, notice.vars);
}

function roleLabel(role) {
  switch (role) {
    case "User":
      return t("role.user");
    case "Assistant":
      return t("role.assistant");
    case "System":
      return t("role.system");
    default:
      return role || "";
  }
}

function updateTabCounts() {
  const ingestAttention = state.ingestionJobs.filter((job) => {
    const status = String(job.status || "");
    return status === "Pending" || status === "Processing" || status === "Failed";
  }).length;
  const counts = {
    candidates: state.candidates.length,
    conflicts: state.conflicts.length,
    ingestion: ingestAttention,
  };

  document.querySelectorAll("[data-count-for]").forEach((el) => {
    const count = counts[el.dataset.countFor] || 0;
    el.hidden = count === 0;
    el.textContent = String(count);
    el.classList.toggle("is-alert", el.dataset.countFor === "conflicts" && count > 0);
  });
}

function formatWhen(value) {
  if (!value) {
    return "";
  }

  return new Date(value).toLocaleString(localeTag(), { hour: "2-digit", minute: "2-digit", day: "numeric", month: "numeric" });
}

async function refreshHealth() {
  try {
    const status = await api("/runtime/status", { timeoutMs: 8000 });
    state.runtime = status;
    applyRuntimeHealth(status);
    if (state.tab === "runtime" && !state.runtimeBusy) {
      renderRuntime();
    }
    return;
  } catch {
    state.runtime = null;
  }

  try {
    const apiHealth = await api("/health", { timeoutMs: 8000 });
    els.health.dataset.state = apiHealth?.status === "Healthy" ? "ok" : "bad";
    els.health.textContent = apiHealth?.status === "Healthy" ? t("health.api") : t("health.apiProblem");
  } catch {
    els.health.dataset.state = "bad";
    els.health.textContent = t("health.apiDown");
  }

  try {
    const database = await api("/health/database", { timeoutMs: 5000 });
    if (els.health.dataset.state === "ok" && database?.status === "Healthy") {
      els.health.textContent = t("health.apiDb");
    } else if (els.health.dataset.state === "ok") {
      els.health.dataset.state = "bad";
      els.health.textContent = t("health.apiProblem");
    }
  } catch {
    if (els.health.dataset.state === "ok") {
      els.health.dataset.state = "bad";
      els.health.textContent = t("health.apiProblem");
    }
  }
}

function applyRuntimeHealth(status) {
  const memoryLive = status.memory?.status === "live";
  const modelLive = status.model?.status === "live";
  const workerLive = status.workers?.status === "live";
  if (status.overall === "Healthy" && memoryLive && modelLive && workerLive) {
    els.health.dataset.state = "ok";
    els.health.textContent = t("health.allOk");
    return;
  }

  els.health.dataset.state = memoryLive ? "warn" : "bad";
  els.health.textContent = [
    memoryLive ? "Nemoryn" : t("health.memoryDown"),
    modelLive ? t("health.model") : (status.model?.status === "unconfigured" ? t("health.modelOff") : t("health.modelDown")),
    workerLive ? t("health.worker") : (status.workers?.status === "starting" ? t("health.workerStart") : t("health.workerDown")),
  ].join(" · ");
}

async function loadPolicy() {
  try {
    state.policy = await api("/memory-policy", { timeoutMs: 8000 });
    renderPolicyChip();
    renderPolicy();
  } catch {
    state.policy = null;
    renderPolicyChip();
    renderPolicy();
  }
}

async function loadTools() {
  try {
    state.tools = await api("/tools", { timeoutMs: 8000 }) || [];
  } catch {
    state.tools = [];
  }

  try {
    const profiles = await api("/tool-permission-profiles", { timeoutMs: 8000 });
    state.permissionProfiles = Array.isArray(profiles)
      ? profiles.filter((profile) => profile.enabled)
      : [];
  } catch {
    state.permissionProfiles = [
      {
        id: "Safe",
        label: t("profile.Safe"),
        description: t("profile.Safe.desc"),
        enabled: true,
      },
    ];
  }

  if (!state.permissionProfiles.some((profile) => profile.id === state.permissionProfile)) {
    state.permissionProfile = "Safe";
  }

  renderAgent();
}

function policyLabel(kind) {
  switch (kind) {
    case "Conservative":
      return t("policy.conservative");
    case "Aggressive":
      return t("policy.aggressive");
    case "Balanced":
      return t("policy.balanced");
    default:
      return kind || t("policy.fallback");
  }
}

function toolTrustLabel(tool) {
  const trust = tool.trust || "";
  const capabilities = Array.isArray(tool.capabilities) ? tool.capabilities.join(", ") : "";
  return [trust, capabilities].filter(Boolean).join(" · ");
}

function permissionProfileLabel(profile) {
  const key = `profile.${profile}`;
  const translated = t(key);
  if (translated !== key) {
    return translated;
  }

  const match = (state.permissionProfiles || []).find((item) => item.id === profile);
  if (match?.label) {
    return match.label;
  }

  return profile || t("profile.Safe");
}

function permissionProfileDescription(profile) {
  const key = `profile.${profile.id}.desc`;
  const translated = t(key);
  if (translated !== key) {
    return translated;
  }

  return profile.description || "";
}

function renderPolicyChip() {
  const kind = state.policy?.policy;
  els.policyChip.textContent = kind ? policyLabel(kind) : t("policy.fallback");
  els.policyChip.dataset.policy = kind || "";
}

function renderPolicy() {
  const policy = state.policy;
  if (!policy) {
    els.panelPolicy.innerHTML = emptyState(t("policy.emptyTitle"), t("policy.emptyDetail"));
    return;
  }

  const thresholds = policy.thresholds || {};
  els.panelPolicy.innerHTML = `
    <p class="muted">${escapeHtml(t("policy.intro"))}</p>
    <div class="policy-choices">
      ${(policy.available || ["Conservative", "Balanced", "Aggressive"]).map((kind) => `
        <button type="button" class="policy-choice ${kind === policy.policy ? "is-selected" : ""}" data-policy="${escapeHtml(kind)}">
          <strong>${escapeHtml(policyLabel(kind))}</strong>
          <small>${escapeHtml(kind)}</small>
        </button>
      `).join("")}
    </div>
    <article class="card">
      <h2>${escapeHtml(policyLabel(policy.policy))}</h2>
      <p>${escapeHtml(policy.summary || "")}</p>
      <h3>${escapeHtml(t("policy.staging"))}</h3>
      <ul class="policy-effects">
        ${(policy.stagingEffects || []).map((effect) => `<li>${escapeHtml(effect)}</li>`).join("")}
      </ul>
      <h3>${escapeHtml(t("policy.recall"))}</h3>
      <ul class="policy-effects">
        ${(policy.recallEffects || []).map((effect) => `<li>${escapeHtml(effect)}</li>`).join("")}
      </ul>
    </article>
    <article class="card">
      <h2>${escapeHtml(t("policy.thresholds"))}</h2>
      <dl class="policy-thresholds">
        <div><dt>${escapeHtml(t("policy.persist"))}</dt><dd>${escapeHtml(thresholds.minPersistConfidence)} / ${escapeHtml(thresholds.minPersistImportance)}</dd></div>
        <div><dt>${escapeHtml(t("policy.autoPromote"))}</dt><dd>${escapeHtml(t("policy.autoPromoteValue", { conf: thresholds.autoPromoteConfidence, imp: thresholds.autoPromoteImportance, evidence: thresholds.autoPromoteEvidenceCount }))}</dd></div>
        <div><dt>${escapeHtml(t("policy.conflict"))}</dt><dd>${escapeHtml(t("policy.conflictValue", { conf: thresholds.contradictionConfidenceThreshold, limit: thresholds.contradictionCandidateLimit }))}</dd></div>
        <div><dt>${escapeHtml(t("policy.recall"))}</dt><dd>${escapeHtml(t("policy.recallValue", { sim: formatScore(thresholds.minRelevantSimilarity), score: formatScore(thresholds.minRelevantScore), relevant: thresholds.maxRelevantMemories }))}</dd></div>
        <div><dt>${escapeHtml(t("policy.core"))}</dt><dd>${escapeHtml(t("policy.coreValue", { imp: thresholds.coreImportanceThreshold, limit: thresholds.coreMemoryLimit }))}</dd></div>
      </dl>
    </article>
  `;
}

function renderBackup() {
  const exported = state.backupExport;
  const backupDocument = state.backupDocument;
  const preview = state.backupPreview;
  const ownerMismatch = backupDocument && state.ownerId && backupDocument.ownerId && backupDocument.ownerId !== state.ownerId;

  els.panelBackup.innerHTML = `
    <p class="muted">${escapeHtml(t("backup.intro"))}</p>
    <div class="explicit-form">
      <div class="row-actions">
        <button type="button" data-action="backup-export"${!state.ownerId || state.backupBusy ? " disabled" : ""}>${state.backupBusy ? t("backup.working") : t("backup.export")}</button>
      </div>
      ${exported ? renderBackupExportSummary(exported) : `<p class="empty">${state.ownerId ? t("backup.willDownload") : t("backup.openOwner")}</p>`}
    </div>
    <form id="backup-import-form" class="explicit-form">
      <label>
        ${escapeHtml(t("backup.fileLabel"))}
        <input type="file" name="backupFile" accept="application/json,.json">
      </label>
      <p class="muted">${state.backupFileName ? escapeHtml(state.backupFileName) : t("backup.fileHint")}</p>
      <label class="cleanup-choice">
        <input type="checkbox" name="skipDuplicates"${state.backupSkipDuplicates ? " checked" : ""}>
        <span>
          <strong>${escapeHtml(t("backup.skipDupTitle"))}</strong>
          ${escapeHtml(t("backup.skipDupDetail"))}
        </span>
      </label>
      <label class="cleanup-choice">
        <input type="checkbox" name="pinExplicit"${state.backupPinExplicit ? " checked" : ""}>
        <span>
          <strong>${escapeHtml(t("backup.pinTitle"))}</strong>
          ${escapeHtml(t("backup.pinDetail"))}
        </span>
      </label>
      ${ownerMismatch ? `<p class="danger-text">${escapeHtml(t("backup.ownerMismatch", { from: backupDocument.ownerId, to: state.ownerId }))}</p>` : ""}
      <div class="row-actions">
        <button type="submit" data-action="backup-preview"${!state.ownerId || !backupDocument || state.backupBusy ? " disabled" : ""}>${escapeHtml(t("backup.preview"))}</button>
        <button type="button" class="ghost" data-action="backup-apply"${!state.ownerId || !backupDocument || !preview || state.backupBusy ? " disabled" : ""}>${escapeHtml(t("backup.import"))}</button>
      </div>
    </form>
    ${preview ? renderBackupImportResult(preview) : `<p class="empty">${backupDocument ? t("backup.previewHint") : t("backup.pickFile")}</p>`}
  `;
}

function renderBackupExportSummary(document) {
  return `
    <article class="card">
      <h2>${escapeHtml(t("backup.lastExport"))}</h2>
      <dl class="backup-counts">
        <div><dt>${escapeHtml(t("backup.owner"))}</dt><dd>${escapeHtml(document.ownerId)}</dd></div>
        <div><dt>${escapeHtml(t("backup.policy"))}</dt><dd>${escapeHtml(document.policy || "-")}</dd></div>
        <div><dt>${escapeHtml(t("backup.memories"))}</dt><dd>${escapeHtml((document.memories || []).length)}</dd></div>
        <div><dt>${escapeHtml(t("backup.candidates"))}</dt><dd>${escapeHtml((document.candidates || []).length)}</dd></div>
        <div><dt>${escapeHtml(t("backup.conflicts"))}</dt><dd>${escapeHtml((document.conflicts || []).length)}</dd></div>
        <div><dt>${escapeHtml(t("backup.audit"))}</dt><dd>${escapeHtml(document.audit?.exportedCount ?? 0)}</dd></div>
      </dl>
    </article>
  `;
}

function renderBackupImportResult(result) {
  const issues = result.issues || [];
  return `
    <article class="card">
      <h2>${result.dryRun ? escapeHtml(t("backup.previewTitle")) : escapeHtml(t("backup.importDone"))}</h2>
      <p class="muted">${result.dryRun ? t("backup.nothingWritten") : t("backup.imported")}</p>
      <dl class="backup-counts">
        ${renderBackupCount(t("backup.conversations"), result.conversations)}
        ${renderBackupCount(t("backup.memories"), result.memories)}
        ${renderBackupCount(t("backup.candidates"), result.candidates)}
        ${renderBackupCount(t("backup.conflicts"), result.conflicts)}
      </dl>
      ${issues.length === 0 ? "" : `
        <h3>${escapeHtml(t("backup.issues"))}</h3>
        <ul class="policy-effects">
          ${issues.map((issue) => `<li>${escapeHtml(issue.kind)} · ${escapeHtml(issue.reason)}</li>`).join("")}
        </ul>
      `}
    </article>
  `;
}

function renderBackupCount(label, counts) {
  return `<div><dt>${escapeHtml(label)}</dt><dd>${escapeHtml(t("backup.countLine", { created: counts?.created ?? 0, skipped: counts?.skipped ?? 0, failed: counts?.failed ?? 0 }))}</dd></div>`;
}

function downloadJson(filename, data) {
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

function parseBackupDocument(raw) {
  let parsed;
  try {
    parsed = JSON.parse(raw);
  } catch {
    throw new Error(t("error.invalidJson"));
  }

  if (!parsed || parsed.format !== "memory.profile.v1") {
    throw new Error(t("error.invalidBackup"));
  }

  return parsed;
}

async function runBackupImport(dryRun) {
  if (!state.ownerId || !state.backupDocument) {
    throw new Error(t("error.needOwnerAndFile"));
  }

  state.backupBusy = true;
  renderBackup();
  try {
    state.backupPreview = await api(`/owners/${encodeURIComponent(state.ownerId)}/memory-import`, {
      method: "POST",
      body: JSON.stringify({
        document: state.backupDocument,
        dryRun,
        skipDuplicates: state.backupSkipDuplicates,
        pinExplicit: state.backupPinExplicit,
      }),
    });
    showError("");
    if (!dryRun) {
      await loadConversations();
      await loadOwners();
      await loadWorkspace();
    }
  } finally {
    state.backupBusy = false;
    renderBackup();
  }
}

async function loadConversations() {
  if (!state.ownerId) {
    state.conversations = [];
    renderConversations();
    return;
  }

  state.conversations = await api(`/owners/${encodeURIComponent(state.ownerId)}/conversations`);
  if (!state.conversations.some((item) => item.id === state.conversationId)) {
    state.conversationId = state.conversations[0]?.id || "";
    localStorage.setItem("memory.conversationId", state.conversationId);
  }

  renderConversations();
}

async function loadOwners() {
  try {
    const owners = await api("/owners", { timeoutMs: 4000 });
    state.owners = Array.isArray(owners) ? owners : [];
  } catch {
    state.owners = [];
  }

  renderOwnerOptions();
}

function renderOwnerOptions() {
  const owners = state.owners || [];
  if (els.ownerOptions) {
    els.ownerOptions.innerHTML = owners.map((owner) => {
      const id = owner.ownerId || "";
      return `<option value="${escapeHtml(id)}" label="${escapeHtml(ownerOptionLabel(owner))}"></option>`;
    }).join("");
  }

  if (!els.ownerPick) {
    return;
  }

  els.ownerPick.hidden = owners.length === 0;
  const current = state.ownerId || "";
  els.ownerPick.innerHTML = `<option value="">${escapeHtml(t("owner.existing"))}</option>` + owners.map((owner) => {
    const id = owner.ownerId || "";
    const selected = id === current ? " selected" : "";
    return `<option value="${escapeHtml(id)}"${selected}>${escapeHtml(ownerOptionLabel(owner))}</option>`;
  }).join("");
}

function ownerOptionLabel(owner) {
  const id = owner.ownerId || "";
  const chats = owner.conversationCount ?? 0;
  const memories = owner.memoryCount ?? 0;
  return t("owner.option", { id, chats, memories });
}

async function loadWorkspace() {
  const conversation = selectedConversation();
  if (!conversation) {
    state.messages = [];
    state.memories = [];
    state.candidates = [];
    state.conflicts = [];
    state.ingestionJobs = [];
    state.auditLogs = [];
    state.toolAuditLogs = [];
    state.cleanupPreview = null;
    state.prepared = null;
    if (state.ownerId) {
      try {
        const [review, auditLogs, toolAuditLogs] = await Promise.all([
          api(`/owners/${encodeURIComponent(state.ownerId)}/memory-review`),
          api(`/owners/${encodeURIComponent(state.ownerId)}/memory-audit?take=80`),
          loadToolAudit(`/owners/${encodeURIComponent(state.ownerId)}/tool-audit?take=80`),
        ]);
        applyReview(review);
        state.auditLogs = Array.isArray(auditLogs) ? auditLogs : [];
        state.toolAuditLogs = Array.isArray(toolAuditLogs) ? toolAuditLogs : [];
      } catch (error) {
        showError(error.message);
      }
    }

    renderWorkspace();
    return;
  }

  const [messages, review, ingestionJobs, auditLogs, toolAuditLogs] = await Promise.all([
    api(`/conversations/${conversation.id}/messages`),
    api(`/owners/${encodeURIComponent(conversation.ownerId)}/memory-review?conversationId=${conversation.id}`),
    api(`/conversations/${conversation.id}/ingestion-jobs?take=40`),
    api(`/owners/${encodeURIComponent(conversation.ownerId)}/memory-audit?take=80`),
    loadToolAudit(`/owners/${encodeURIComponent(conversation.ownerId)}/tool-audit?conversationId=${conversation.id}&take=80`),
  ]);

  state.messages = messages;
  applyReview(review);
  state.ingestionJobs = ingestionJobs;
  state.auditLogs = Array.isArray(auditLogs) ? auditLogs : [];
  state.toolAuditLogs = Array.isArray(toolAuditLogs) ? toolAuditLogs : [];
  renderWorkspace();
}

function applyReview(review) {
  state.memories = review?.activeMemories || [];
  state.candidates = review?.pendingCandidates || [];
  state.conflicts = review?.pendingConflicts || [];
  state.cleanupPreview = review?.cleanup || null;
}

function renderConversations() {
  if (!state.ownerId) {
    els.conversationList.innerHTML = `<li>${emptyState(t("conversations.pickTitle"), t("conversations.pickDetail"))}</li>`;
    return;
  }

  if (state.conversations.length === 0) {
    els.conversationList.innerHTML = `<li>${emptyState(t("conversations.emptyTitle"), t("conversations.emptyDetail"))}</li>`;
    return;
  }

  els.conversationList.innerHTML = state.conversations.map((conversation) => `
    <li>
      <button class="item ${conversation.id === state.conversationId ? "is-selected" : ""}" data-id="${conversation.id}" type="button" ${conversation.id === state.conversationId ? 'aria-current="true"' : ""}>
        <span class="item-title">${escapeHtml(conversation.title || conversation.externalId)}</span>
        <small>${escapeHtml(conversation.externalId)}${conversation.updatedAt ? ` · ${escapeHtml(formatWhen(conversation.updatedAt))}` : ""}</small>
      </button>
    </li>
  `).join("");
}

function renderWorkspace() {
  const conversation = selectedConversation();
  els.chatTitle.textContent = conversation ? (conversation.title || conversation.externalId) : t("conversations.title");
  els.chatMeta.textContent = conversation ? conversation.id : t("chat.openFirst");

  if (state.messages.length === 0) {
    els.messages.innerHTML = state.agentBusy
      ? `<article class="bubble pending" data-role="Assistant"><div class="who">${escapeHtml(t("chat.agent"))}</div><p>${escapeHtml(t("chat.thinking"))}</p></article>`
      : emptyState(t("chat.startTitle"), t("chat.startDetail"));
  } else {
    els.messages.innerHTML = `${state.messages.map((message) => `
      <article class="bubble" data-role="${escapeHtml(message.role)}">
        <div class="who">${escapeHtml(roleLabel(message.role))} · ${escapeHtml(formatWhen(message.occurredAt))}</div>
        <p>${escapeHtml(message.content)}</p>
      </article>
    `).join("")}${state.agentBusy ? `
      <article class="bubble pending" data-role="Assistant">
        <div class="who">${escapeHtml(t("chat.agent"))}</div>
        <p>${escapeHtml(t("chat.thinking"))}</p>
      </article>
    ` : ""}`;
    els.messages.scrollTop = els.messages.scrollHeight;
  }

  els.composer.querySelector("button[type='submit']").disabled = state.agentBusy;
  els.messageRole.disabled = state.agentBusy;
  els.messageContent.disabled = state.agentBusy;

  renderMemories();
  renderCandidates();
  renderConflicts();
  renderIngestion();
  renderAudit();
  renderCleanup();
  renderEval();
  renderPolicy();
  renderBackup();
  renderAgent();
  renderRuntime();
  updateTabCounts();
}

function renderMemories() {
  const cards = state.memories.map((memory) => `
    <article class="card" data-memory-id="${memory.id}">
      <div class="badges">
        <span class="badge">${escapeHtml(memory.type)}</span>
        <span class="badge">${escapeHtml(memory.scope)}</span>
        <span class="badge">${escapeHtml(memory.origin)}</span>
        ${memory.isPinned ? `<span class="badge pin">pinned</span>` : ""}
      </div>
      <h2>${escapeHtml(memory.content)}</h2>
      <small>imp ${memory.importance} · conf ${memory.confidence}</small>
      <div class="row-actions">
        <button type="button" class="quiet" data-action="pin">${memory.isPinned ? t("memory.unpin") : t("memory.pin")}</button>
        <button type="button" class="quiet" data-action="toggle-correct">${t("memory.edit")}</button>
        <button type="button" class="danger" data-action="forget">${t("memory.forget")}</button>
      </div>
      <form class="correct-row" hidden>
        <input name="content" value="${escapeHtml(memory.content)}">
        <button type="submit">${t("memory.save")}</button>
      </form>
    </article>
  `).join("");

  els.panelMemories.innerHTML = `
    <form class="explicit-form" id="explicit-memory-form">
      <textarea name="content" rows="2" placeholder="${escapeHtml(t("memory.explicitPlaceholder"))}"></textarea>
      <div class="row-actions">
        <select name="type">
          <option>Fact</option>
          <option>Preference</option>
          <option>Goal</option>
          <option>Constraint</option>
          <option>Relationship</option>
        </select>
        <label class="muted"><input type="checkbox" name="pin"> pin</label>
        <button type="submit">${t("memory.write")}</button>
      </div>
    </form>
    ${cards || emptyState(t("memory.emptyTitle"), t("memory.emptyDetail"))}
  `;
}

function renderCandidates() {
  if (state.candidates.length === 0) {
    els.panelCandidates.innerHTML = emptyState(t("candidates.emptyTitle"), t("candidates.emptyDetail"));
    return;
  }

  els.panelCandidates.innerHTML = state.candidates.map((candidate) => `
    <article class="card" data-candidate-id="${candidate.id}">
      <div class="badges">
        <span class="badge">${escapeHtml(candidate.type)}</span>
        <span class="badge">${escapeHtml(candidate.scope)}</span>
        <span class="badge">${escapeHtml(t("candidates.evidence", { count: candidate.evidenceCount }))}</span>
      </div>
      <h2>${escapeHtml(candidate.content)}</h2>
      <small>imp ${candidate.importance} · conf ${candidate.confidence}</small>
      <div class="row-actions">
        <button type="button" data-action="promote">${t("candidates.promote")}</button>
        <button type="button" class="quiet" data-action="promote-pin">${t("candidates.promotePin")}</button>
        <button type="button" class="danger" data-action="reject">${t("candidates.reject")}</button>
      </div>
    </article>
  `).join("");
}

function renderConflicts() {
  if (state.conflicts.length === 0) {
    els.panelConflicts.innerHTML = emptyState(t("conflicts.emptyTitle"), t("conflicts.emptyDetail"));
    return;
  }

  els.panelConflicts.innerHTML = state.conflicts.map((conflict) => `
    <article class="card" data-conflict-id="${conflict.id}">
      <small>${escapeHtml(conflict.reason || t("conflicts.reason"))}</small>
      <h2>${escapeHtml(t("conflicts.new", { content: conflict.candidate.content }))}</h2>
      <p class="muted">${escapeHtml(t("conflicts.existing", { content: conflict.conflictingMemory.content }))}</p>
      <div class="row-actions">
        <button type="button" data-action="accept">${t("conflicts.accept")}</button>
        <button type="button" class="ghost" data-action="keep">${t("conflicts.keep")}</button>
      </div>
    </article>
  `).join("");
}

function renderIngestion() {
  const counts = state.ingestionJobs.reduce((result, job) => {
    result[job.status] = (result[job.status] || 0) + 1;
    return result;
  }, {});

  const summary = ["Pending", "Processing", "Failed", "Skipped", "Succeeded"]
    .filter((status) => counts[status])
    .map((status) => `<span class="badge status-${status.toLowerCase()}">${status} ${counts[status]}</span>`)
    .join("");

  if (state.ingestionJobs.length === 0) {
    els.panelIngestion.innerHTML = emptyState(t("ingestion.emptyTitle"), t("ingestion.emptyDetail"));
    return;
  }

  els.panelIngestion.innerHTML = `
    <div class="job-summary">${summary || `<span class="badge">0</span>`}</div>
    ${state.ingestionJobs.map((job) => `
      <article class="card">
        <div class="badges">
          <span class="badge status-${escapeHtml(String(job.status).toLowerCase())}">${escapeHtml(job.status)}</span>
          <span class="badge">${escapeHtml(t("ingestion.attempts", { count: job.attemptCount }))}</span>
        </div>
        <h2>${escapeHtml(job.messageId)}</h2>
        <small>${escapeHtml(t("ingestion.created", { when: formatWhen(job.createdAt) }))} · ${escapeHtml(t("ingestion.updated", { when: formatWhen(job.updatedAt) }))}</small>
        ${job.lockedUntil ? `<p class="muted">${escapeHtml(t("ingestion.locked", { when: formatWhen(job.lockedUntil) }))}</p>` : ""}
        ${job.lastError ? `<pre class="prompt error-text">${escapeHtml(job.lastError)}</pre>` : ""}
      </article>
    `).join("")}
  `;
}

function renderAudit() {
  if (!state.ownerId) {
    els.panelAudit.innerHTML = emptyState(t("audit.pickTitle"), t("audit.pickDetail"));
    return;
  }

  const memoryCards = state.auditLogs.map((entry) => `
    <article class="card">
      <div class="badges">
        <span class="badge">${escapeHtml(entry.action)}</span>
        <span class="badge">${escapeHtml(entry.actorKind)}</span>
      </div>
      <h2>${escapeHtml(entry.reason || entry.action)}</h2>
      <small>${escapeHtml(formatWhen(entry.occurredAt))} · ${escapeHtml(entry.actorId)}</small>
      ${entry.details ? `<p class="muted">${escapeHtml(entry.details)}</p>` : ""}
    </article>
  `).join("");

  const toolCards = state.toolAuditLogs.map((entry) => `
    <article class="card">
      <div class="badges">
        <span class="badge">${escapeHtml(entry.outcome)}</span>
        ${entry.trust ? `<span class="badge">${escapeHtml(entry.trust)}</span>` : ""}
        ${(entry.capabilities || []).map((capability) => `<span class="badge">${escapeHtml(capability)}</span>`).join("")}
        ${entry.invoked ? `<span class="badge">${t("audit.invoked")}</span>` : `<span class="badge">${t("audit.blocked")}</span>`}
      </div>
      <h2>${escapeHtml(entry.name || t("audit.unnamed"))}</h2>
      <small>${escapeHtml(formatWhen(entry.occurredAt))} · ${escapeHtml(entry.callId || "")}</small>
      ${entry.error ? `<pre class="prompt error-text">${escapeHtml(entry.error)}</pre>` : ""}
      ${entry.result ? `<pre class="prompt">${escapeHtml(entry.result)}</pre>` : ""}
    </article>
  `).join("");

  if (!memoryCards && !toolCards) {
    els.panelAudit.innerHTML = emptyState(t("audit.emptyTitle"), t("audit.emptyDetail"));
    return;
  }

  els.panelAudit.innerHTML = `
    <h3>${escapeHtml(t("audit.tools"))}</h3>
    ${toolCards || `<p class="empty">${t("audit.noTools")}</p>`}
    <h3>${escapeHtml(t("audit.memories"))}</h3>
    ${memoryCards || `<p class="empty">${t("audit.noMemories")}</p>`}
  `;
}

function renderCleanup() {
  if (!state.ownerId) {
    els.panelCleanup.innerHTML = emptyState(t("cleanup.pickTitle"), t("cleanup.pickDetail"));
    return;
  }

  const preview = state.cleanupPreview;
  if (!preview) {
    els.panelCleanup.innerHTML = `
      <form id="cleanup-preview-form" class="explicit-form">
        <p class="muted">${escapeHtml(t("cleanup.previewHint"))}</p>
        <button type="submit">${t("cleanup.find")}</button>
      </form>
    `;
    return;
  }

  const suggestions = preview.suggestions || [];
  if (suggestions.length === 0) {
    els.panelCleanup.innerHTML = `
      <form id="cleanup-preview-form" class="explicit-form">
        <p class="muted">${escapeHtml(t("cleanup.scannedEmpty", { count: preview.scannedCount }))}</p>
        <button type="submit">${t("cleanup.reload")}</button>
      </form>
    `;
    return;
  }

  els.panelCleanup.innerHTML = `
    <form id="cleanup-apply-form">
      <div class="explicit-form">
        <p class="muted">${escapeHtml(t("cleanup.scanned", { count: preview.scannedCount }))}</p>
        <div class="row-actions">
          <button type="submit">${t("cleanup.archive")}</button>
          <button type="button" class="ghost" data-action="cleanup-refresh">${t("cleanup.reload")}</button>
        </div>
      </div>
      ${suggestions.map((suggestion) => `
        <article class="card">
          <label class="cleanup-choice">
            <input type="checkbox" name="memoryId" value="${escapeHtml(suggestion.memoryId)}">
            <span>
              <span class="badge">${escapeHtml(suggestion.reasonCode)}</span>
              <strong>${escapeHtml(suggestion.memory.content)}</strong>
              <small>${escapeHtml(suggestion.reason)} · imp ${escapeHtml(suggestion.memory.importance)} · conf ${escapeHtml(suggestion.memory.confidence)}</small>
            </span>
          </label>
        </article>
      `).join("")}
    </form>
  `;
}

function renderAgent() {
  const prepared = state.prepared;
  const result = state.lastAgentResult;
  const tools = Array.isArray(state.tools) ? state.tools : [];
  const toolTrace = Array.isArray(result?.toolTrace) ? result.toolTrace : [];
  els.panelAgent.innerHTML = `
    <form id="prepare-form" class="explicit-form">
      <textarea name="userMessage" rows="2" placeholder="${escapeHtml(t("agent.placeholder"))}"></textarea>
      <button type="submit">${t("agent.prepare")}</button>
    </form>
    ${tools.length ? `
      <article class="card">
        <h2>${escapeHtml(t("agent.tools", { count: tools.length }))}</h2>
        <ul class="tool-list">
          ${tools.map((tool) => `
            <li>
              <code>${escapeHtml(tool.name)}</code>
              <small>${escapeHtml(toolTrustLabel(tool))}</small>
              <span>${escapeHtml(tool.description || "")}</span>
            </li>
          `).join("")}
        </ul>
        ${state.permissionProfiles.length ? `
          <fieldset class="tool-profiles">
            <legend>${escapeHtml(t("agent.permissions"))}</legend>
            ${state.permissionProfiles.map((profile) => `
              <label class="cleanup-choice">
                <input type="radio" name="permissionProfile" value="${escapeHtml(profile.id)}"${state.permissionProfile === profile.id ? " checked" : ""}>
                <span>
                  <strong>${escapeHtml(permissionProfileLabel(profile.id))}</strong>
                  <small>${escapeHtml(permissionProfileDescription(profile))}</small>
                </span>
              </label>
            `).join("")}
          </fieldset>
        ` : ""}
      </article>
    ` : `<p class="empty">${t("agent.noTools")}</p>`}
    ${result ? `
      <article class="card ${result.status === "Failed" ? "failed" : ""}">
        <h2>${escapeHtml(t("agent.lastRun"))}</h2>
        <small>
          ${escapeHtml(t("agent.meta", {
            status: result.status,
            provider: result.provider || "-",
            model: result.model || "-",
            contract: result.preparedTurn?.promptContractVersion || "-",
            policy: policyLabel(result.preparedTurn?.policy),
            core: result.preparedTurn?.coreMemories?.length ?? 0,
            relevant: result.preparedTurn?.relevantMemories?.length ?? 0,
            tools: toolTrace.length,
            profile: permissionProfileLabel(result.permissionProfile),
          }))}
        </small>
        ${result.errorMessage ? `<p class="danger-text">${escapeHtml(result.errorMessage)}</p>` : ""}
        ${toolTrace.length ? `
          <ul class="tool-trace">
            ${toolTrace.map((item) => `
              <li>
                <small>
                  <code>${escapeHtml(item.name)}</code>
                  · ${item.ok ? t("agent.ok") : t("agent.error")}
                </small>
                <pre class="prompt">${escapeHtml(item.result || "")}</pre>
              </li>
            `).join("")}
          </ul>
        ` : ""}
      </article>
    ` : ""}
    ${prepared ? `
      <article class="card">
        <h2>${escapeHtml(t("agent.systemPrompt"))}</h2>
        <small>
          ${escapeHtml(t("agent.promptMeta", {
            contract: prepared.promptContractVersion || "-",
            policy: policyLabel(prepared.policy),
          }))}
        </small>
        <pre class="prompt">${escapeHtml(prepared.systemPromptBlock || t("agent.emptyPrompt"))}</pre>
      </article>
      <article class="card">
        <h2>${escapeHtml(t("agent.core", { count: prepared.coreMemories.length }))}</h2>
        ${renderMemoryDiagnostics(prepared.coreMemories)}
      </article>
      <article class="card">
        <h2>${escapeHtml(t("agent.relevant", { count: prepared.relevantMemories.length }))}</h2>
        ${renderMemoryDiagnostics(prepared.relevantMemories)}
      </article>
    ` : `<p class="empty">${t("agent.previewHint")}</p>`}
  `;
}

function renderRuntime() {
  const status = state.runtime;
  els.panelRuntime.innerHTML = `
    <p class="muted">${escapeHtml(t("runtime.intro"))}</p>
    <div class="row-actions">
      <button type="button" data-action="runtime-refresh"${state.runtimeBusy ? " disabled" : ""}>${state.runtimeBusy ? t("runtime.checking") : t("runtime.refresh")}</button>
    </div>
    ${renderDatabaseConnectionForm()}
    ${renderAiConnectionForm()}
    ${status ? renderRuntimeBody(status) : `<p class="empty">${t("runtime.statusEmpty")}</p>`}
  `;
}

function renderDatabaseConnectionForm() {
  const connection = state.databaseConnection;
  const draft = state.databaseConnectionDraft;
  const host = draft?.host ?? connection?.host ?? "";
  const port = draft?.port ?? connection?.port ?? 5432;
  const database = draft?.database ?? connection?.database ?? "";
  const username = draft?.username ?? connection?.username ?? "";
  const password = draft?.password ?? "";
  const busy = state.runtimeBusy;
  const persisted = connection?.persisted === true;
  const reachable = connection?.reachable === true;
  const dockerPortMismatch = String(host).trim().toLowerCase() === "postgres" && Number(port) !== 5432;
  return `
    <form id="database-connection-form" class="explicit-form runtime-connection">
      <h2>${escapeHtml(t("runtime.db.title"))}</h2>
      <p class="muted">${t("runtime.db.intro")}</p>
      <label>
        ${escapeHtml(t("runtime.db.host"))}
        <input name="host" required placeholder="postgres" value="${escapeHtml(host)}"${busy ? " disabled" : ""}>
      </label>
      <label>
        ${escapeHtml(t("runtime.db.port"))}
        <input name="port" type="number" min="1" max="65535" required value="${escapeHtml(port)}"${busy ? " disabled" : ""}>
      </label>
      <label>
        ${escapeHtml(t("runtime.db.database"))}
        <input name="database" required placeholder="nemoryn" value="${escapeHtml(database)}"${busy ? " disabled" : ""}>
      </label>
      <label>
        ${escapeHtml(t("runtime.db.user"))}
        <input name="username" required placeholder="nemoryn" value="${escapeHtml(username)}"${busy ? " disabled" : ""}>
      </label>
      <label>
        ${escapeHtml(t("runtime.db.password"))}
        <input name="password" type="password" autocomplete="new-password" placeholder="${connection?.passwordSet ? t("runtime.db.keepPassword") : t("runtime.db.passwordPh")}" value="${escapeHtml(password)}"${busy ? " disabled" : ""}>
      </label>
      ${state.databaseConnectionNotice ? `<p class="ok-text">${escapeHtml(noticeText(state.databaseConnectionNotice))}</p>` : ""}
      ${dockerPortMismatch ? `<p class="danger-text">${escapeHtml(t("runtime.db.dockerPortHint"))}</p>` : ""}
      ${reachable ? `<p class="ok-text">${t("runtime.db.ok")}</p>` : connection ? `<p class="danger-text">${escapeHtml(connection.reachError || t("runtime.db.unreachable"))}</p>` : ""}
      ${persisted ? `<p class="muted">${t("runtime.db.persisted")}</p>` : `<p class="muted">${t("runtime.db.fromConfig")}</p>`}
      ${connection?.persistError ? `<p class="danger-text">${escapeHtml(t("runtime.db.persistError", { error: connection.persistError }))}</p>` : ""}
      <div class="row-actions">
        <button type="submit"${busy ? " disabled" : ""}>${t("runtime.db.save")}</button>
      </div>
    </form>
  `;
}

function renderAiConnectionForm() {
  const connection = state.aiConnection;
  const draft = state.aiConnectionDraft;
  const baseUrl = draft?.baseUrl ?? connection?.baseUrl ?? "";
  const chatModel = draft?.chatModel ?? connection?.chatModel ?? "";
  const embeddingModel = draft?.embeddingModel ?? connection?.embeddingModel ?? "";
  const models = uniqueModelNames([chatModel, ...(connection?.chatModels || [])]);
  const busy = state.runtimeBusy;
  const persisted = connection?.persisted === true;
  return `
    <form id="ai-connection-form" class="explicit-form runtime-connection">
      <h2>${escapeHtml(t("runtime.ai.title"))}</h2>
      <p class="muted">${escapeHtml(t("runtime.ai.intro", { provider: connection?.provider || "—" }))}</p>
      ${models.length > 0 ? `
        <div class="model-choices">
          ${models.map((name) => `
            <button type="button" class="policy-choice ${name === chatModel ? "is-selected" : ""}" data-chat-model="${escapeHtml(name)}"${busy ? " disabled" : ""}>
              <strong>${escapeHtml(name)}</strong>
              <small>${name === chatModel ? t("runtime.ai.default") : t("runtime.ai.use")}</small>
            </button>
          `).join("")}
        </div>
        <input type="hidden" name="chatModel" value="${escapeHtml(chatModel)}">
      ` : `
        <label>
          ${escapeHtml(t("runtime.ai.chatModel"))}
          <input name="chatModel" required placeholder="gpt-oss:20b" value="${escapeHtml(chatModel)}"${busy ? " disabled" : ""}>
        </label>
      `}
      <label>
        ${escapeHtml(t("runtime.ai.baseUrl"))}
        <input name="baseUrl" type="url" required placeholder="http://192.168.1.2:11434" value="${escapeHtml(baseUrl)}"${busy ? " disabled" : ""}>
      </label>
      <label>
        ${escapeHtml(t("runtime.ai.embedding"))}
        <input name="embeddingModel" list="ai-embedding-models" required placeholder="nomic-embed-text" value="${escapeHtml(embeddingModel)}"${busy ? " disabled" : ""}>
        <datalist id="ai-embedding-models">
          ${uniqueModelNames([embeddingModel, ...models]).map((name) => `<option value="${escapeHtml(name)}"></option>`).join("")}
        </datalist>
      </label>
      ${state.aiConnectionNotice ? `<p class="ok-text">${escapeHtml(noticeText(state.aiConnectionNotice))}</p>` : ""}
      ${persisted ? `<p class="muted">${t("runtime.db.persisted")}</p>` : `<p class="muted">${t("runtime.ai.fromConfig")}</p>`}
      ${connection?.persistError ? `<p class="danger-text">${escapeHtml(t("runtime.db.persistError", { error: connection.persistError }))}</p>` : ""}
      ${connection?.modelsError ? `<p class="danger-text">${escapeHtml(connection.modelsError)}</p>` : ""}
      <div class="row-actions">
        <button type="submit" data-action="runtime-save-connection"${busy ? " disabled" : ""}>${t("runtime.ai.saveUrl")}</button>
        <button type="button" class="ghost" data-action="runtime-load-models"${busy ? " disabled" : ""}>${t("runtime.ai.refreshList")}</button>
      </div>
    </form>
  `;
}

function uniqueModelNames(names) {
  const seen = new Set();
  const result = [];
  for (const name of names) {
    const trimmed = String(name || "").trim();
    if (!trimmed) {
      continue;
    }

    const key = trimmed.toLowerCase();
    if (seen.has(key)) {
      continue;
    }

    seen.add(key);
    result.push(trimmed);
  }

  return result;
}

function readDatabaseConnectionForm(form) {
  return {
    host: form.host.value.trim(),
    port: Number(form.port.value) || 5432,
    database: form.database.value.trim(),
    username: form.username.value.trim(),
    password: form.password.value,
  };
}

async function saveDatabaseConnection(request) {
  state.databaseConnectionDraft = request;
  state.runtimeBusy = true;
  renderRuntime();
  try {
    state.databaseConnection = await api("/runtime/database-connection", {
      method: "PUT",
      timeoutMs: 30000,
      body: JSON.stringify(request),
    });
    state.databaseConnectionDraft = null;
    await refreshHealth();
    showError("");
    if (state.databaseConnection?.persistError) {
      state.databaseConnectionNotice = "";
    } else if (state.databaseConnection?.reachable) {
      state.databaseConnectionNotice = state.databaseConnection.persisted
        ? { key: "runtime.db.savedPersisted" }
        : { key: "runtime.db.savedLive" };
    } else {
      state.databaseConnectionNotice = "";
    }
  } catch (error) {
    showError(error.message);
  } finally {
    state.runtimeBusy = false;
    renderRuntime();
  }
}

function readAiConnectionForm(form) {
  return {
    baseUrl: form.baseUrl.value.trim(),
    chatModel: form.chatModel.value.trim(),
    embeddingModel: form.embeddingModel.value.trim(),
  };
}

function renderRuntimeBody(status) {
  const memory = status.memory || {};
  const model = status.model || {};
  const workers = status.workers || {};
  return `
    <p class="muted">${escapeHtml(t("runtime.overall", { overall: status.overall || "-", when: formatWhen(status.checkedAt) || t("runtime.now") }))}</p>
    <div class="runtime-grid">
      ${renderRuntimeCard(t("runtime.memory"), memory.status, memory.summary, `
        <dl class="backup-counts">
          <div><dt>${escapeHtml(t("runtime.database"))}</dt><dd>${memory.databaseReachable ? t("runtime.live") : t("runtime.down")}</dd></div>
          <div><dt>${escapeHtml(t("runtime.migrations"))}</dt><dd>${escapeHtml(memory.latestMigration || "-")}</dd></div>
          <div><dt>${escapeHtml(t("runtime.candidates"))}</dt><dd>${escapeHtml(memory.pendingCandidates ?? 0)}</dd></div>
          <div><dt>${escapeHtml(t("runtime.conflicts"))}</dt><dd>${escapeHtml(memory.pendingConflicts ?? 0)}</dd></div>
        </dl>
      `)}
      ${renderRuntimeCard(t("runtime.model"), model.status, model.summary, `
        <dl class="backup-counts">
          <div><dt>${escapeHtml(t("runtime.provider"))}</dt><dd>${escapeHtml(model.provider || "-")}</dd></div>
          <div><dt>${escapeHtml(t("runtime.baseUrl"))}</dt><dd>${escapeHtml(model.baseUrl || "-")}</dd></div>
          <div><dt>${escapeHtml(t("runtime.chat"))}</dt><dd>${escapeHtml(model.chatModel || "-")} · ${model.chatAvailable ? "on" : "off"}</dd></div>
          <div><dt>${escapeHtml(t("runtime.embedding"))}</dt><dd>${escapeHtml(model.embeddingModel || "-")} · ${model.embeddingAvailable ? "on" : "off"}</dd></div>
          <div><dt>${escapeHtml(t("runtime.reachable"))}</dt><dd>${model.providerReachable ? `${t("runtime.yes")}${model.providerLatencyMs == null ? "" : ` · ${escapeHtml(model.providerLatencyMs)} ms`}` : t("runtime.no")}</dd></div>
        </dl>
        ${model.providerError ? `<p class="danger-text">${escapeHtml(model.providerError)}</p>` : ""}
      `)}
      ${renderRuntimeCard(t("runtime.worker"), workers.status, workers.summary, `
        <dl class="backup-counts">
          <div><dt>${escapeHtml(t("runtime.pendingJobs"))}</dt><dd>${escapeHtml(workers.pendingIngestionJobs ?? 0)}</dd></div>
          <div><dt>${escapeHtml(t("runtime.processing"))}</dt><dd>${escapeHtml(workers.processingIngestionJobs ?? 0)}</dd></div>
        </dl>
        ${renderRuntimeWorker(workers.ingestion)}
        ${renderRuntimeWorker(workers.retention)}
      `)}
    </div>
  `;
}

function renderRuntimeCard(title, status, summary, body) {
  return `
    <article class="card runtime-card" data-state="${escapeHtml(status || "down")}">
      <div class="badges">
        <span class="badge runtime-${escapeHtml(status || "down")}">${escapeHtml(runtimeStatusLabel(status))}</span>
      </div>
      <h2>${escapeHtml(title)}</h2>
      <p>${escapeHtml(summary || "")}</p>
      ${body}
    </article>
  `;
}

function renderRuntimeWorker(worker) {
  if (!worker) {
    return "";
  }

  return `
    <div class="memory-diagnostic">
      <div class="badges">
        <span class="badge">${escapeHtml(worker.name || "worker")}</span>
        <span class="badge runtime-${escapeHtml(worker.status || "down")}">${escapeHtml(runtimeStatusLabel(worker.status))}</span>
      </div>
      <small>
        last ${escapeHtml(formatWhen(worker.lastAttemptAt) || t("runtime.never"))}
        ${worker.lastSucceeded === false ? t("runtime.lastFailed") : ""}
      </small>
      ${worker.lastError ? `<p class="danger-text">${escapeHtml(worker.lastError)}</p>` : ""}
    </div>
  `;
}

function runtimeStatusLabel(status) {
  switch (status) {
    case "live":
      return t("runtime.status.live");
    case "starting":
      return t("runtime.status.starting");
    case "stalled":
      return t("runtime.status.stalled");
    case "unconfigured":
      return t("runtime.status.unconfigured");
    default:
      return t("runtime.status.down");
  }
}

async function loadRuntime() {
  if (state.runtimeBusy) {
    return;
  }

  state.runtimeBusy = true;
  renderRuntime();
  try {
    await Promise.all([refreshHealth(), loadAiConnection(), loadDatabaseConnection()]);
    showError("");
  } catch (error) {
    showError(error.message);
  } finally {
    state.runtimeBusy = false;
    renderRuntime();
  }
}

async function loadAiConnection() {
  state.aiConnection = await api("/runtime/ai-connection", { timeoutMs: 8000 });
  state.aiConnectionDraft = null;
}

async function loadDatabaseConnection() {
  state.databaseConnection = await api("/runtime/database-connection", { timeoutMs: 8000 });
  state.databaseConnectionDraft = null;
}

async function currentAiConnectionRequest(overrides = {}) {
  const form = document.querySelector("#ai-connection-form");
  const fromForm = form ? readAiConnectionForm(form) : null;
  const connection = state.aiConnection || {};
  const draft = state.aiConnectionDraft || {};
  return {
    baseUrl: overrides.baseUrl ?? fromForm?.baseUrl ?? draft.baseUrl ?? connection.baseUrl ?? "",
    chatModel: overrides.chatModel ?? fromForm?.chatModel ?? draft.chatModel ?? connection.chatModel ?? "",
    embeddingModel: overrides.embeddingModel ?? fromForm?.embeddingModel ?? draft.embeddingModel ?? connection.embeddingModel ?? "",
  };
}

async function saveAiConnection(request, options = {}) {
  const { refreshStatus = false, notice } = options;
  state.aiConnectionDraft = request;
  state.runtimeBusy = true;
  renderRuntime();
  try {
    state.aiConnection = await api("/runtime/ai-connection", {
      method: "PUT",
      timeoutMs: 8000,
      body: JSON.stringify(request),
    });
    state.aiConnectionDraft = null;
    if (refreshStatus) {
      await refreshHealth();
    }
    showError("");
    if (state.aiConnection?.persistError) {
      state.aiConnectionNotice = "";
    } else if (notice) {
      state.aiConnectionNotice = notice;
    } else if (state.aiConnection?.persisted) {
      state.aiConnectionNotice = { key: "runtime.ai.savedPersisted" };
    } else {
      state.aiConnectionNotice = { key: "runtime.ai.savedLive" };
    }
  } catch (error) {
    showError(error.message);
  } finally {
    state.runtimeBusy = false;
    renderRuntime();
  }
}

function renderMemoryDiagnostics(items) {
  if (!items || items.length === 0) {
    return `<p class="muted">${t("agent.noneSelected")}</p>`;
  }

  return items.map((item) => `
    <div class="memory-diagnostic">
      <div class="badges">
        <span class="badge">${escapeHtml(item.selectionKind || "Memory")}</span>
        <span class="badge">${escapeHtml(item.type)}</span>
        <span class="badge">${escapeHtml(item.scope)}</span>
        <span class="badge">${escapeHtml(item.origin)}</span>
        ${item.isPinned ? `<span class="badge pin">pinned</span>` : ""}
      </div>
      <strong>${escapeHtml(item.content)}</strong>
      <small>
        score ${escapeHtml(formatScore(item.score))}
        ${item.similarity == null ? "" : `· sim ${escapeHtml(formatScore(item.similarity))}`}
        · imp ${escapeHtml(item.importance)}
        · conf ${escapeHtml(item.confidence)}
      </small>
      <p class="muted">${escapeHtml(item.selectionReason || t("agent.selectedByRanking"))}</p>
    </div>
  `).join("");
}

function formatScore(value) {
  if (value == null || Number.isNaN(Number(value))) {
    return "-";
  }

  return Number(value).toFixed(2);
}

function formatPercent(value) {
  if (value == null || Number.isNaN(Number(value))) {
    return "-";
  }

  return `${Math.round(Number(value) * 100)}%`;
}

function parseEvalRequest(raw) {
  let parsed;
  try {
    parsed = JSON.parse(raw);
  } catch {
    throw new Error(t("error.evalJson"));
  }

  if (Array.isArray(parsed)) {
    return { limit: 8, cases: parsed };
  }

  if (parsed && Array.isArray(parsed.cases)) {
    return {
      limit: parsed.limit ?? 8,
      cases: parsed.cases,
    };
  }

  if (parsed && typeof parsed.query === "string") {
    return { limit: parsed.limit ?? 8, cases: [parsed] };
  }

  throw new Error(t("error.evalShape"));
}

function renderEval() {
  const conversation = selectedConversation();
  const scenarios = state.evalScenarios.trim() ? state.evalScenarios : defaultEvalScenarios;
  const result = state.evalResult;

  els.panelEval.innerHTML = `
    <form id="eval-form" class="explicit-form">
      <p class="muted">${conversation
        ? t("eval.introReady")
        : t("eval.introNeedChat")}</p>
      <textarea name="scenarios" class="eval-json" rows="12" spellcheck="false">${escapeHtml(scenarios)}</textarea>
      <div class="row-actions">
        <button type="submit"${!conversation || state.evalBusy ? " disabled" : ""}>${state.evalBusy ? t("eval.running") : t("eval.run")}</button>
        <button type="button" class="ghost" data-action="eval-reset">${t("eval.insertSample")}</button>
      </div>
    </form>
    ${result ? renderEvalResult(result) : `<p class="empty">${conversation ? t("eval.resultHint") : t("eval.openFirst")}</p>`}
  `;
}

function renderEvalResult(result) {
  const summary = result.summary || {};
  const thresholds = result.thresholds || {};

  return `
    <div class="eval-summary">
      <span class="badge">${escapeHtml(policyLabel(thresholds.policy))}</span>
      <span class="badge ${summary.hitRate === 1 ? "hit" : summary.misses ? "miss" : ""}">hit rate ${escapeHtml(formatPercent(summary.hitRate))}</span>
      <span class="badge">MRR ${escapeHtml(formatScore(summary.meanReciprocalRank))}</span>
      <span class="badge hit">hits ${escapeHtml(summary.hits ?? 0)}</span>
      <span class="badge miss">misses ${escapeHtml(summary.misses ?? 0)}</span>
      <span class="badge">cases ${escapeHtml(summary.evaluableCaseCount ?? 0)}/${escapeHtml(summary.caseCount ?? 0)}</span>
    </div>
    <p class="muted">
      sim ≥ ${escapeHtml(formatScore(thresholds.minRelevantSimilarity))}
      · score ≥ ${escapeHtml(formatScore(thresholds.minRelevantScore))}
      · core ${escapeHtml(thresholds.coreMemoryLimit ?? "-")}
      · relevant ${escapeHtml(thresholds.maxRelevantMemories ?? "-")}
      · embeddings ${thresholds.embeddingAvailable ? t("eval.embeddingsOn") : t("eval.embeddingsOff")}
    </p>
    ${(result.cases || []).map(renderEvalCase).join("")}
  `;
}

function renderEvalCase(recallCase) {
  const hitClass = recallCase.hasExpectation ? (recallCase.hit ? "hit" : "miss") : "";
  const hitLabel = !recallCase.hasExpectation
    ? t("eval.noExpectation")
    : recallCase.hit
      ? "hit"
      : "miss";

  return `
    <article class="card ${recallCase.hasExpectation && !recallCase.hit ? "failed" : ""}">
      <div class="badges">
        <span class="badge ${hitClass}">${escapeHtml(hitLabel)}</span>
        ${recallCase.id ? `<span class="badge">${escapeHtml(recallCase.id)}</span>` : ""}
        ${recallCase.bestExpectedRank ? `<span class="badge">rank ${escapeHtml(recallCase.bestExpectedRank)}</span>` : ""}
        ${recallCase.reciprocalRank == null ? "" : `<span class="badge">RR ${escapeHtml(formatScore(recallCase.reciprocalRank))}</span>`}
      </div>
      <h2>${escapeHtml(recallCase.query)}</h2>
      ${renderEvalExpectations(recallCase.expectations)}
      <small>${escapeHtml(t("eval.selectedCount", { count: (recallCase.selected || []).length }))}</small>
      ${renderEvalSelected(recallCase.selected)}
    </article>
  `;
}

function renderEvalExpectations(expectations) {
  if (!expectations || expectations.length === 0) {
    return "";
  }

  return expectations.map((expectation) => `
    <div class="memory-diagnostic">
      <div class="badges">
        <span class="badge ${expectation.found ? "hit" : "miss"}">${expectation.found ? "found" : "miss"}</span>
        ${expectation.selectionKind ? `<span class="badge">${escapeHtml(expectation.selectionKind)}</span>` : ""}
        ${expectation.rank ? `<span class="badge">rank ${escapeHtml(expectation.rank)}</span>` : ""}
      </div>
      <strong>${escapeHtml(expectation.content || expectation.expectedContentContains || expectation.memoryId || t("eval.expectedMemory"))}</strong>
      <small>
        ${expectation.expectedContentContains ? escapeHtml(t("eval.contains", { text: expectation.expectedContentContains })) : ""}
        score ${escapeHtml(formatScore(expectation.score))}
        ${expectation.similarity == null ? "" : `· sim ${escapeHtml(formatScore(expectation.similarity))}`}
      </small>
      ${expectation.missReason ? `<p class="danger-text">${escapeHtml(expectation.missReason)}</p>` : ""}
    </div>
  `).join("");
}

function renderEvalSelected(selected) {
  if (!selected || selected.length === 0) {
    return `<p class="muted">${t("eval.nothingSelected")}</p>`;
  }

  return selected.map((item) => {
    const memory = item.memory || {};
    return `
      <div class="memory-diagnostic">
        <div class="badges">
          <span class="badge">#${escapeHtml(item.rank)}</span>
          <span class="badge">${escapeHtml(memory.selectionKind || "Memory")}</span>
          <span class="badge">${escapeHtml(memory.type || "")}</span>
          ${memory.isPinned ? `<span class="badge pin">pinned</span>` : ""}
        </div>
        <strong>${escapeHtml(memory.content)}</strong>
        <small>
          score ${escapeHtml(formatScore(memory.score))}
          ${memory.similarity == null ? "" : `· sim ${escapeHtml(formatScore(memory.similarity))}`}
          · imp ${escapeHtml(memory.importance)}
          · conf ${escapeHtml(memory.confidence)}
        </small>
        <p class="muted">${escapeHtml(memory.selectionReason || "")}</p>
      </div>
    `;
  }).join("");
}

function setTab(tab) {
  state.tab = tab;
  document.querySelectorAll(".tab").forEach((button) => {
    const active = button.dataset.tab === tab;
    button.classList.toggle("is-active", active);
    button.setAttribute("aria-selected", active ? "true" : "false");
  });
  els.panelMemories.hidden = tab !== "memories";
  els.panelCandidates.hidden = tab !== "candidates";
  els.panelConflicts.hidden = tab !== "conflicts";
  els.panelIngestion.hidden = tab !== "ingestion";
  els.panelAudit.hidden = tab !== "audit";
  els.panelCleanup.hidden = tab !== "cleanup";
  els.panelEval.hidden = tab !== "eval";
  els.panelPolicy.hidden = tab !== "policy";
  els.panelBackup.hidden = tab !== "backup";
  els.panelAgent.hidden = tab !== "agent";
  els.panelRuntime.hidden = tab !== "runtime";
  if (tab === "runtime") {
    loadRuntime();
  }
}

function startPolling() {
  window.clearInterval(state.pollTimer);
  let remaining = 8;
  state.pollTimer = window.setInterval(async () => {
    remaining -= 1;
    try {
      await loadWorkspace();
      showError("");
    } catch (error) {
      showError(error.message);
    }

    if (remaining <= 0) {
      window.clearInterval(state.pollTimer);
    }
  }, 1500);
}

els.ownerForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await openOwner(els.ownerId.value);
});

els.ownerPick.addEventListener("change", async () => {
  const ownerId = els.ownerPick.value.trim();
  if (!ownerId) {
    return;
  }

  await openOwner(ownerId);
});

async function openOwner(ownerId) {
  state.ownerId = String(ownerId || "").trim();
  els.ownerId.value = state.ownerId;
  localStorage.setItem("memory.ownerId", state.ownerId);
  state.cleanupPreview = null;
  renderOwnerOptions();
  try {
    showError("");
    await loadConversations();
    await loadWorkspace();
  } catch (error) {
    showError(error.message);
  }
}

els.newConversation.addEventListener("click", () => {
  els.newConversationForm.hidden = false;
  if (!els.newExternalId.value) {
    els.newExternalId.value = `chat-${Date.now()}`;
  }
});

els.cancelNewConversation.addEventListener("click", () => {
  els.newConversationForm.hidden = true;
});

els.newConversationForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (!state.ownerId) {
    showError(t("error.needOwnerId"));
    return;
  }

  try {
    const conversation = await api(
      `/conversations/by-external-id/${encodeURIComponent(els.newExternalId.value.trim())}`,
      {
        method: "PUT",
        body: JSON.stringify({
          ownerId: state.ownerId,
          title: els.newTitle.value.trim() || null,
        }),
      },
    );
    state.conversationId = conversation.id;
    localStorage.setItem("memory.conversationId", conversation.id);
    els.newConversationForm.hidden = true;
    els.newTitle.value = "";
    showError("");
    await loadConversations();
    await loadOwners();
    await loadWorkspace();
  } catch (error) {
    showError(error.message);
  }
});

els.conversationList.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-id]");
  if (!button) {
    return;
  }

  state.conversationId = button.dataset.id;
  localStorage.setItem("memory.conversationId", state.conversationId);
  renderConversations();
  try {
    showError("");
    await loadWorkspace();
  } catch (error) {
    showError(error.message);
  }
});

els.composer.addEventListener("submit", async (event) => {
  event.preventDefault();
  const conversation = selectedConversation();
  const content = els.messageContent.value.trim();
  if (!conversation || !content) {
    return;
  }

  const submittedAsUser = els.messageRole.value === "User";
  try {
    if (submittedAsUser) {
      state.messages = [
        ...state.messages,
        {
          id: "pending-user",
          role: "User",
          content,
          occurredAt: new Date().toISOString(),
        },
      ];
      state.agentBusy = true;
      showError("");
      renderWorkspace();

      const result = await api(`/conversations/${conversation.id}/agent/chat`, {
        method: "POST",
        timeoutMs: 120000,
        body: JSON.stringify({
          message: content,
          memoryLimit: 8,
          recentMessageCount: 12,
          permissionProfile: state.permissionProfile || "Safe",
        }),
      });
      state.lastAgentResult = result;
      state.prepared = result.preparedTurn || null;
      state.permissionProfile = "Safe";
      if (result.status === "Failed") {
        showError(t("error.messageSavedNoModel", { detail: result.errorMessage || t("error.unknown") }));
      }
    } else {
      await api(`/conversations/${conversation.id}/messages`, {
        method: "POST",
        body: JSON.stringify({
          role: els.messageRole.value,
          content,
          externalId: null,
          occurredAt: null,
        }),
      });
      state.lastAgentResult = null;
    }

    els.messageContent.value = "";
    if (!state.lastAgentResult || state.lastAgentResult.status !== "Failed") {
      showError("");
    }

    await loadWorkspace();
    startPolling();
  } catch (error) {
    showError(error.message);
    if (submittedAsUser) {
      try {
        await loadWorkspace();
      } catch {
        renderWorkspace();
      }
    }
  } finally {
    state.agentBusy = false;
    renderWorkspace();
  }
});

document.querySelector(".tabs").addEventListener("click", (event) => {
  const tab = event.target.closest(".tab");
  if (tab) {
    setTab(tab.dataset.tab);
  }
});

els.messageContent.addEventListener("keydown", (event) => {
  if (event.key !== "Enter" || event.shiftKey || event.isComposing) {
    return;
  }

  event.preventDefault();
  els.composer.requestSubmit();
});

els.health.addEventListener("click", () => {
  setTab("runtime");
});

els.panelRuntime.addEventListener("input", (event) => {
  const databaseForm = event.target.closest("#database-connection-form");
  if (databaseForm) {
    state.databaseConnectionDraft = readDatabaseConnectionForm(databaseForm);
    return;
  }

  const form = event.target.closest("#ai-connection-form");
  if (!form) {
    return;
  }

  state.aiConnectionDraft = readAiConnectionForm(form);
});

els.panelRuntime.addEventListener("change", (event) => {
  const databaseForm = event.target.closest("#database-connection-form");
  if (databaseForm) {
    state.databaseConnectionDraft = readDatabaseConnectionForm(databaseForm);
    return;
  }

  const form = event.target.closest("#ai-connection-form");
  if (!form) {
    return;
  }

  state.aiConnectionDraft = readAiConnectionForm(form);
});

els.panelRuntime.addEventListener("submit", async (event) => {
  if (event.target.id === "database-connection-form") {
    event.preventDefault();
    if (state.runtimeBusy) {
      return;
    }

    await saveDatabaseConnection(readDatabaseConnectionForm(event.target));
    return;
  }

  if (event.target.id !== "ai-connection-form") {
    return;
  }

  event.preventDefault();
  if (state.runtimeBusy) {
    return;
  }

  await saveAiConnection(readAiConnectionForm(event.target), { refreshStatus: true });
});

els.panelRuntime.addEventListener("click", async (event) => {
  const modelButton = event.target.closest("button[data-chat-model]");
  if (modelButton) {
    event.preventDefault();
    if (state.runtimeBusy) {
      return;
    }

    const chatModel = modelButton.dataset.chatModel;
    if (!chatModel) {
      return;
    }

    const request = await currentAiConnectionRequest({ chatModel });
    await saveAiConnection(request, { notice: { key: "runtime.ai.modelDefault", vars: { model: chatModel } } });
    return;
  }

  const button = event.target.closest("button[data-action]");
  if (!button || state.runtimeBusy) {
    return;
  }

  if (button.dataset.action === "runtime-refresh" || button.dataset.action === "runtime-load-models") {
    await loadRuntime();
  }
});

els.panelMemories.addEventListener("submit", async (event) => {
  event.preventDefault();
  const conversation = selectedConversation();
  if (event.target.id === "explicit-memory-form" && conversation) {
    const form = event.target;
    const content = form.content.value.trim();
    if (!content) {
      return;
    }

    try {
      await api("/memories", {
        method: "POST",
        body: JSON.stringify({
          conversationId: conversation.id,
          content,
          type: form.type.value,
          importance: 0.9,
          confidence: 0.95,
          sourceMessageId: null,
          sourceSummary: null,
          validFrom: null,
          validUntil: null,
          sourceMetadataJson: null,
          pin: form.pin.checked,
        }),
      });
      showError("");
      await loadWorkspace();
    } catch (error) {
      showError(error.message);
    }
    return;
  }

  const card = event.target.closest("[data-memory-id]");
  if (!card || !event.target.matches("form.correct-row")) {
    return;
  }

  try {
    await api(`/memories/${card.dataset.memoryId}`, {
      method: "PUT",
      body: JSON.stringify({ content: event.target.content.value, pin: null }),
    });
    showError("");
    await loadWorkspace();
  } catch (error) {
    showError(error.message);
  }
});

els.panelMemories.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action]");
  const card = event.target.closest("[data-memory-id]");
  if (!button || !card) {
    return;
  }

  const id = card.dataset.memoryId;
  try {
    if (button.dataset.action === "pin") {
      const memory = state.memories.find((item) => item.id === id);
      await api(`/memories/${id}/${memory?.isPinned ? "unpin" : "pin"}`, { method: "POST" });
    } else if (button.dataset.action === "forget") {
      if (!window.confirm(t("confirm.forget"))) {
        return;
      }
      await api(`/memories/${id}`, { method: "DELETE" });
    } else if (button.dataset.action === "toggle-correct") {
      const form = card.querySelector("form.correct-row");
      form.hidden = !form.hidden;
      return;
    }

    showError("");
    await loadWorkspace();
  } catch (error) {
    showError(error.message);
  }
});

els.panelCandidates.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action]");
  const card = event.target.closest("[data-candidate-id]");
  if (!button || !card) {
    return;
  }

  const id = card.dataset.candidateId;
  try {
    if (button.dataset.action === "reject") {
      await api(`/memory-candidates/${id}/reject`, { method: "POST" });
    } else {
      await api(`/memory-candidates/${id}/promote`, {
        method: "POST",
        body: JSON.stringify({ pin: button.dataset.action === "promote-pin" }),
      });
    }
    showError("");
    await loadWorkspace();
  } catch (error) {
    showError(error.message);
  }
});

els.panelConflicts.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action]");
  const card = event.target.closest("[data-conflict-id]");
  if (!button || !card) {
    return;
  }

  const id = card.dataset.conflictId;
  try {
    if (button.dataset.action === "accept") {
      await api(`/memory-conflicts/${id}/accept-candidate`, { method: "POST" });
    } else {
      await api(`/memory-conflicts/${id}/keep-existing`, { method: "POST" });
    }
    showError("");
    await loadWorkspace();
  } catch (error) {
    showError(error.message);
  }
});

els.panelCleanup.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (!state.ownerId) {
    return;
  }

  try {
    if (event.target.id === "cleanup-preview-form") {
      state.cleanupPreview = await api(
        `/owners/${encodeURIComponent(state.ownerId)}/memories/cleanup-preview`,
        { method: "POST" },
      );
      showError("");
      renderCleanup();
      return;
    }

    if (event.target.id === "cleanup-apply-form") {
      const memoryIds = [...event.target.querySelectorAll('input[name="memoryId"]:checked')]
        .map((input) => input.value);
      if (memoryIds.length === 0) {
        showError(t("error.cleanupSelect"));
        return;
      }

      await api(`/owners/${encodeURIComponent(state.ownerId)}/memories/cleanup-apply`, {
        method: "POST",
        body: JSON.stringify({ memoryIds }),
      });
      state.cleanupPreview = null;
      showError("");
      await loadWorkspace();
    }
  } catch (error) {
    showError(error.message);
  }
});

els.panelCleanup.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action='cleanup-refresh']");
  if (!button || !state.ownerId) {
    return;
  }

  try {
    state.cleanupPreview = await api(
      `/owners/${encodeURIComponent(state.ownerId)}/memories/cleanup-preview`,
      { method: "POST" },
    );
    showError("");
    renderCleanup();
  } catch (error) {
    showError(error.message);
  }
});

els.panelAgent.addEventListener("change", (event) => {
  const input = event.target.closest("input[name='permissionProfile']");
  if (!input) {
    return;
  }

  state.permissionProfile = input.value || "Safe";
});

els.panelAgent.addEventListener("submit", async (event) => {
  event.preventDefault();
  const conversation = selectedConversation();
  if (!conversation || event.target.id !== "prepare-form") {
    return;
  }

  try {
    state.prepared = await api(`/conversations/${conversation.id}/agent/prepare`, {
      method: "POST",
      body: JSON.stringify({
        userMessage: event.target.userMessage.value.trim() || null,
        limit: 10,
        recentMessageCount: 12,
      }),
    });
    showError("");
    renderAgent();
  } catch (error) {
    showError(error.message);
  }
});

els.panelEval.addEventListener("input", (event) => {
  if (event.target.name !== "scenarios") {
    return;
  }

  state.evalScenarios = event.target.value;
  localStorage.setItem("memory.evalScenarios", state.evalScenarios);
});

els.panelEval.addEventListener("click", (event) => {
  const button = event.target.closest("button[data-action='eval-reset']");
  if (!button) {
    return;
  }

  state.evalScenarios = defaultEvalScenarios;
  localStorage.setItem("memory.evalScenarios", state.evalScenarios);
  renderEval();
});

els.panelEval.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (event.target.id !== "eval-form") {
    return;
  }

  const conversation = selectedConversation();
  if (!conversation) {
    showError(t("error.needConversation"));
    return;
  }

  const raw = event.target.scenarios.value;
  state.evalScenarios = raw;
  localStorage.setItem("memory.evalScenarios", raw);

  let request;
  try {
    request = parseEvalRequest(raw);
  } catch (error) {
    showError(error.message);
    return;
  }

  try {
    state.evalBusy = true;
    showError("");
    renderEval();
    state.evalResult = await api(`/conversations/${conversation.id}/memories/evaluate-recall`, {
      method: "POST",
      body: JSON.stringify(request),
    });
    showError("");
  } catch (error) {
    showError(error.message);
  } finally {
    state.evalBusy = false;
    renderEval();
  }
});

els.policyChip.addEventListener("click", () => {
  setTab("policy");
});

els.panelPolicy.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-policy]");
  if (!button) {
    return;
  }

  try {
    state.policy = await api("/memory-policy", {
      method: "PUT",
      body: JSON.stringify({ policy: button.dataset.policy }),
    });
    showError("");
    renderPolicyChip();
    renderPolicy();
  } catch (error) {
    showError(error.message);
  }
});

els.panelBackup.addEventListener("change", async (event) => {
  if (event.target.name === "skipDuplicates") {
    state.backupSkipDuplicates = event.target.checked;
    state.backupPreview = null;
    renderBackup();
    return;
  }

  if (event.target.name === "pinExplicit") {
    state.backupPinExplicit = event.target.checked;
    state.backupPreview = null;
    renderBackup();
    return;
  }

  if (event.target.name !== "backupFile") {
    return;
  }

  const file = event.target.files?.[0];
  if (!file) {
    return;
  }

  try {
    state.backupDocument = parseBackupDocument(await file.text());
    state.backupFileName = file.name;
    state.backupPreview = null;
    showError("");
    renderBackup();
  } catch (error) {
    state.backupDocument = null;
    state.backupFileName = "";
    state.backupPreview = null;
    showError(error.message);
    renderBackup();
  }
});

els.panelBackup.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action]");
  const action = button?.dataset.action;
  if (!button || state.backupBusy || (action !== "backup-export" && action !== "backup-apply")) {
    return;
  }

  try {
    if (action === "backup-export") {
      if (!state.ownerId) {
        showError(t("error.needOwner"));
        return;
      }

      state.backupBusy = true;
      renderBackup();
      const exported = await api(`/owners/${encodeURIComponent(state.ownerId)}/memory-export`);
      state.backupExport = exported;
      downloadJson(`memory-${state.ownerId}.json`, exported);
      showError("");
    }

    if (action === "backup-apply") {
      await runBackupImport(false);
    }
  } catch (error) {
    showError(error.message);
  } finally {
    state.backupBusy = false;
    renderBackup();
  }
});

els.panelBackup.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (event.target.id !== "backup-import-form" || state.backupBusy) {
    return;
  }

  try {
    await runBackupImport(true);
  } catch (error) {
    showError(error.message);
    state.backupBusy = false;
    renderBackup();
  }
});

els.ownerId.value = state.ownerId;
applyStaticI18n();
if (els.localeSelect) {
  els.localeSelect.addEventListener("change", () => {
    setLocale(els.localeSelect.value);
    renderOwnerOptions();
    renderConversations();
    renderWorkspace();
    renderPolicyChip();
    if (state.runtime) {
      applyRuntimeHealth(state.runtime);
    } else {
      refreshHealth();
    }
  });
}
setTab("memories");
renderConversations();
renderWorkspace();
renderPolicyChip();
refreshHealth();
window.setInterval(refreshHealth, 15000);
loadPolicy();
loadTools();
loadOwners();
if (state.ownerId) {
  loadConversations()
    .then(loadWorkspace)
    .catch((error) => {
      showError(error.message);
      renderConversations();
      renderWorkspace();
    });
}
