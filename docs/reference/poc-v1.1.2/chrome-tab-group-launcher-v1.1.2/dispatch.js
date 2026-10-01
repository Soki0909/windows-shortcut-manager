const TARGETS = {
  research: "研究",
  seven: "7脱",
};

const statusEl = document.querySelector("#status");
const params = new URLSearchParams(location.search);
const action = params.get("action");
const title = TARGETS[action];

function setStatus(text) {
  statusEl.textContent = text;
}

async function closeDispatcherTab() {
  try {
    const tab = await chrome.tabs.getCurrent();
    if (tab?.id != null) {
      await chrome.tabs.remove(tab.id);
    }
  } catch {}
}

async function run() {
  if (!title) {
    setStatus(`不明なactionです: ${action ?? "(なし)"}`);
    return;
  }

  // WindowsショートカットからChromeを起動した直後でもservice workerが
  // 起動できるよう、extension page自身からruntime messageを送る。
  const response = await chrome.runtime.sendMessage({
    type: "focus-group",
    title,
  });

  if (response?.ok) {
    setStatus(`「${title}」へ移動しました。`);
    await closeDispatcherTab();
    return;
  }

  setStatus(`「${title}」が現在開かれていません。`);
  // service worker側でも通知を出す。不要なDispatcherタブは残さない。
  setTimeout(() => void closeDispatcherTab(), 700);
}

run().catch((error) => {
  setStatus(`Dispatcher error: ${String(error)}`);
});
