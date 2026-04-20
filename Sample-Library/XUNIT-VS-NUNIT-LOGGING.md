# xUnit vs NUnit: Test Logging

How the two frameworks differ in capturing test output, and why it matters when testing MassTransit sagas that use Quartz.

## Short version

**NUnit captures stdout automatically; xUnit doesn't — you have to wire it up.**

---

## NUnit

- `Console.WriteLine`, `TestContext.Out.WriteLine`, and `TestContext.WriteLine` all go to the test's output automatically.
- `TestContext.Out` is static — available anywhere, any thread, no injection required.
- stdout is captured per-test by the runner.
- Result: anything that writes to the console (including default `ILogger` → `ConsoleLoggerProvider` if configured) shows up in the test log with zero wiring.

## xUnit

- No stdout capture by default. `Console.WriteLine` goes nowhere visible in test output (it appears on the build host but not in the test result).
- You must use `ITestOutputHelper`:
  - v2: inject it via constructor (`public MyTests(ITestOutputHelper output)`)
  - v3: `TestContext.Current.TestOutputHelper` (AsyncLocal)
- Nothing captures `ILogger` output automatically — you have to build a provider (`XUnitLoggerProvider`) and register it with `AddLogging(...)` in each test's DI container.

---

## Why this matters for MassTransit + Quartz

- **NUnit tests** typically don't configure logging at all. MassTransit's internal logs go to a default `NullLoggerFactory` — no factory, no test-level visibility, but also no disposal problem because no disposable factory lives in the DI container.
- **xUnit tests** usually add `XUnitLoggerProvider(output)` per test to route logs to the test's `ITestOutputHelper`. That forces a fresh *disposable* `LoggerFactory` per test → Quartz's process-static `LogProvider` caches the first one → disposal on the next test = `ObjectDisposedException`.

The error looks like:

```
System.ObjectDisposedException : Cannot access a disposed object.
Object name: 'LoggerFactory'.
   at Microsoft.Extensions.Logging.LoggerFactory.CreateLogger(String categoryName)
   at Quartz.Simpl.MicrosoftLoggingProvider.GetLogger(String name)
   at Quartz.Logging.LogProvider.GetLogger(String name)
```

### Is it about parallelism?

No. Disabling xUnit parallelism via `xunit.runner.json`:

```json
{
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false
}
```

**does not fix it.** Quartz's static `LogProvider` persists the same reference across sequential tests within one process. Disposal fires between tests regardless of parallelism.

### What actually fixes it

Three options, in order of trade-off:

1. **Drop per-test logging entirely.** Match the NUnit approach. Tests pass; you lose log visibility.
2. **Process-static, non-disposing `ILoggerFactory` + output relay.** Quartz caches a reference that's never disposed; per-test output is routed via a static relay slot the tests update. Tests pass *and* logs work.
3. **Run tests one at a time.** Separate processes = fresh static state per test.

---

## Are there xUnit extensions that mimic NUnit's behavior?

Not really. A few partial options exist:

- **Meziantou.Extensions.Logging.Xunit.v3** — bridges `ILogger` to `ITestOutputHelper`. Doesn't capture `Console.WriteLine`.
- **Divergic.Logging.Xunit** — similar bridge, slightly different API.
- **`Console.SetOut(...)` redirection** — manually redirect stdout to the current test's `ITestOutputHelper` in a fixture. But `Console.Out` is process-static, so concurrent tests collide.
- **xUnit v3 runner-level stdout capture** — doesn't exist as a built-in toggle. v3 has `TestContext.Current.SendDiagnosticMessage`, but that's for diagnostics, not arbitrary stdout.

### Why none of them match NUnit cleanly

NUnit relies on two things xUnit deliberately doesn't do:

1. **Per-test stdout redirection at the runner level.** NUnit hooks `Console.Out` per test via its own `TextWriter`. xUnit refuses because tests run in parallel and `Console.Out` is process-static.
2. **Static `TestContext`.** NUnit gives a global way to reach the current test's output. xUnit v3's `TestContext.Current` exists, but it's AsyncLocal — which fails when work runs on threads the AsyncLocal didn't flow to (e.g. MassTransit's receive pipeline started at bus startup).

Even with the extensions above, logs emitted from MassTransit worker threads won't automatically land in the current test's output. That's why a `TestOutputRelay` pattern is needed — a static slot each test sets, so non-test threads can still reach the right output regardless of AsyncLocal.

---

## Recommended xUnit setup for MassTransit saga tests

```
Per-test DI container ──▶ ILoggerFactory (wrapper)
                              │
                              ▼
                 NonDisposingLoggerFactory  ◀── Dispose() is a no-op
                              │
                              ▼
                 SharedLoggerFactory.Instance  ◀── process-static, never disposed
                              │
                              ▼
                 XUnitLoggerProvider(TestOutputRelay)
                                            │
                                            ▼
                                      TestOutputRelay.Current  ◀── each test sets this in its ctor
```

- Quartz sees a live `ILoggerFactory` forever (through the wrapper).
- Per-test output still reaches the correct `ITestOutputHelper` via the relay.
- No more `ObjectDisposedException`.
