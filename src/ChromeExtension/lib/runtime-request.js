export async function runtimeRequest(send, message, timeoutMs = 5000) {
  let timer;
  try {
    const response = await Promise.race([
      Promise.resolve().then(() => send(message)),
      new Promise(resolve => { timer = setTimeout(() => resolve({ ok: false, code: "message-timeout", message: "応答を確認できませんでした。処理済みの場合もあります。状態を更新してから再試行してください。" }), timeoutMs); }),
    ]);
    return response && typeof response.ok === "boolean" ? response : { ok: false, code: "invalid-response", message: "応答がありません。状態を更新して再試行してください。" };
  } catch {
    return { ok: false, code: "message-failed", message: "拡張との通信に失敗しました。状態を更新して再試行してください。" };
  } finally { clearTimeout(timer); }
}
