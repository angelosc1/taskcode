// Account-only query adapted from Codenotch (MIT); see THIRD-PARTY-NOTICES.md.
// No query(), conversation, prompt, or inference is started.
(async () => {
  let daemon;
  let result;
  try {
    let sdk;
    try { sdk = await import('@qoder-ai/qoder-agent-sdk'); }
    catch { result = { error: 'sdk_missing' }; return; }
    daemon = await sdk.startDaemon({ auth: sdk.qodercliAuth(), initializeTimeoutMs: 60000 });
    if (typeof daemon.auth === 'function' && !await daemon.auth()) {
      result = { error: 'needs_auth' }; return;
    }
    const usage = await daemon.getUsageInfo();
    // Whitelist account metrics before crossing the process boundary.
    result = { usage: Object.fromEntries(['totalUsagePercentage', 'userQuota', 'addOnQuota',
      'orgResourcePackage', 'expiresAt', 'userType', 'isQuotaExceeded']
      .filter(key => usage?.[key] !== undefined).map(key => [key, usage[key]])) };
  } catch { result = { error: 'query_failed' }; }
  finally {
    try { if (daemon) await daemon.stop(); } catch {}
    process.stdout.write(JSON.stringify(result ?? { error: 'query_failed' }), () => process.exit(0));
  }
})();
