const text = (id, value) => {
  const element = document.getElementById(id);
  if (element) element.textContent = value ?? "";
};

const state = (id, value) => {
  const element = document.getElementById(id);
  if (!element) return;
  element.textContent = value ?? "";
  element.dataset.state = value ?? "";
};

const localTime = value =>
  value ? new Date(value).toLocaleString() : "Never";

async function refresh() {
  try {
    const response = await fetch("/ui/v1/status", {
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!response.ok) return;
    const value = await response.json();
    state("sync-state", value.synchronization.state);
    text("sync-last-success", localTime(value.synchronization.lastSuccessfulAt));
    text("sync-error", value.synchronization.lastError);
    state("adapter-state", value.adapter.state);
    text("adapter-name", value.adapter.name);
    text("adapter-version", value.adapter.version);
    text("adapter-last-seen",
      value.adapter.lastSeenAt ? localTime(value.adapter.lastSeenAt) : "Never connected");
    text("update-state", value.update.state);
    text("update-message", value.update.message);
    const progress = document.getElementById("update-progress");
    if (progress) progress.value = value.update.progressPercent;
  } catch {
    state("sync-state", "UI disconnected");
  }
}

refresh();

const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/status")
  .withAutomaticReconnect()
  .build();

connection.on("StatusChanged", change => {
  if (change === "desired-state") {
    window.location.reload();
    return;
  }
  refresh();
});

connection.start().catch(() => {
  state("sync-state", "UI disconnected");
});

window.setInterval(refresh, 30000);
