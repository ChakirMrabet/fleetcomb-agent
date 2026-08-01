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

const activeUpdateStates = new Set([
  "Downloading", "Verified", "Installing", "AwaitingAdapter"
]);

const setUpdateControls = update => {
  const active = activeUpdateStates.has(update.state);
  document.querySelectorAll("[data-update-form] button").forEach(button => {
    button.disabled = active;
  });
};

const updateInventory = applications => {
  for (const application of applications ?? []) {
    const id = application.applicationId.replaceAll("-", "");
    text(`installed-${id}`, application.installedVersion);
    const form = document.querySelector(
      `[data-update-form][data-application-id="${application.applicationId}"]`);
    if (form && form.dataset.availableVersion.toLowerCase() ===
        application.installedVersion.toLowerCase()) {
      form.replaceWith("Up to date");
    }
  }
};

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
    setUpdateControls(value.update);
    updateInventory(value.installedApplications);
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

document.querySelectorAll("[data-update-form]").forEach(form => {
  form.addEventListener("submit", async event => {
    event.preventDefault();
    const button = form.querySelector("button");
    button.disabled = true;
    text("update-message", "Starting update...");
    try {
      const response = await fetch(form.action, {
        method: "POST",
        credentials: "same-origin",
        body: new FormData(form),
        headers: { Accept: "application/json" }
      });
      const result = await response.json();
      if (!response.ok)
        throw new Error(result.message ?? result.title ?? "Update failed.");
      await refresh();
    } catch (error) {
      text("update-message", error.message);
      button.disabled = false;
    }
  });
});
