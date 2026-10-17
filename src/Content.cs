using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NexusCore
{
    /// <summary>One rendered terminal line plus the semantic colour it uses.</summary>
    internal sealed class Line
    {
        public readonly string Text;
        public readonly string Kind;

        public Line(string text, string kind)
        {
            Text = text;
            Kind = kind;
        }
    }

    /// <summary>
    /// Ported from panel.py's data pools and line generators. Same phrases,
    /// same probabilities, same colour kinds - so the C# saver and the Python
    /// one show the same world.
    /// </summary>
    internal static class Content
    {
        // ── pools ────────────────────────────────────────────────────────
        public static readonly string[] Projects = {
            "nexus-core", "phantom-relay", "obsidian-mesh", "spectre-9", "vault-sigma",
            "echo-lattice", "umbra-link", "cipher-node", "ghost-protocol", "dark-fiber",
            "kronos-engine", "hydra-cluster", "odin-framework", "styx-pipeline",
        };

        public static readonly string[] Namespaces = {
            "prod-shadow-7", "staging-omega", "classified-alpha", "black-site-3",
            "orbital-relay", "deep-state-11", "zone-49", "sublevel-kappa",
        };

        public static readonly string[] RustFiles = {
            "src/core/inference.rs", "src/mesh/relay.rs", "src/crypto/cipher.rs",
            "src/net/transport.rs", "src/auth/biometric.rs", "src/pipeline/stage.rs",
            "src/signal/processor.rs", "src/index/trie.rs", "src/graph/traverse.rs",
            "src/scheduler/queue.rs", "src/wasm/runtime.rs", "src/neural/bridge.rs",
        };

        public static readonly string[] TsFiles = {
            "src/components/Dashboard.tsx", "src/hooks/useWebSocket.ts",
            "src/api/client.ts", "src/store/reducers.ts", "src/utils/crypto.ts",
            "src/workers/inference.worker.ts", "lib/protocol/handshake.ts",
        };

        public static readonly string[] GoFiles = {
            "pkg/relay/handler.go", "internal/mesh/peer.go", "cmd/server/main.go",
            "pkg/crypto/aes.go", "internal/store/badger.go", "pkg/pubsub/nats.go",
        };

        public static readonly string[] PyFiles = {
            "models/transformer.py", "training/distributed.py", "inference/serve.py",
            "pipeline/etl.py", "analysis/anomaly.py", "utils/embeddings.py",
        };

        public static readonly string[] AllFiles;   // filled in the static ctor
        public static readonly string[] Ips;

        public static readonly string[] DockerImages = {
            "nexus/cortex:3.7.2", "shadow/inference:v4.1-rc", "umbra/mesh:nightly-0201",
            "phantom/neural:edge-gpu", "classified/scan:2.1.0", "ghost/relay:stable",
        };

        public static readonly string[] Pods = {
            "cortex", "relay", "scanner", "watcher", "indexer", "classifier",
            "predictor", "ingester", "scheduler", "gateway", "encoder", "decoder",
        };

        public static readonly string[] DbTables = {
            "subjects", "observations", "intercepts", "profiles", "predictions",
            "connections", "events", "anomalies", "directives", "embeddings",
            "sessions", "audit_log", "feature_vectors", "model_weights",
        };

        public static readonly string[] ApiEndpoints = {
            "v1/telemetry/ingest", "v2/prediction/run", "v1/metrics/rollup",
            "v3/identity/verify", "v1/infra/node/heartbeat", "v2/analysis/trace",
        };

        public static readonly string[] Ciphers = {
            "AES-256-GCM", "ChaCha20-Poly1305", "X25519", "RSA-4096", "Kyber-1024", "Ed25519",
        };

        public static readonly string[] PacketFlags = { "SYN", "ACK", "PSH", "FIN", "RST", "URG" };
        public static readonly string[] LintTools = { "eslint", "ruff", "clippy", "pylint", "prettier", "golangci-lint" };
        public static readonly string[] Pkgs = { "serde", "tokio", "fastapi", "axum", "tracing", "sqlalchemy", "numpy", "react" };

        public static readonly string[] Ominous = {
            "CONNECTING TO CLASSIFIED//NOFORN ENDPOINT...",
            "biometric scan complete. subject: VERONICA. status: OBSERVED",
            "deploying to PROD-SHADOW-7... authorization: EYES ONLY",
            "ALERT: unauthorized presence detected in sector 4 (confidence: 0.97)",
            "syncing behavioral prediction model... subject profile updated",
            "NOTE: observation window for VERONICA extended to 2026-Q3",
            "decrypting sigint intercept... source: DOMESTIC/PASSIVE",
            "WARNING: cognitive pattern deviation detected. flagging for review.",
            "MEMO: Project LOOKING GLASS status -> ACTIVE. do not discuss offline.",
            "routing through airgapped relay... plausible deniability: MAINTAINED",
            "subject VERONICA: ambient audio capture -> NOMINAL",
            "cross-referencing social graph... 3 new connections flagged",
            "PRIORITY: handler check-in overdue. initiating dead drop protocol.",
            "firmware update pushed to all smart home devices. monitoring resumed.",
            "behavioral model accuracy: 94.7%. recommend continued observation.",
            "NOTICE: this terminal is being monitored by 3 external entities",
            "project MOCKINGBIRD status: all assets in position",
            "thermal signature detected 2.1m from this workstation",
            "subject VERONICA: sentiment analysis indicates SUSPICION (0.34). within tolerance.",
            "VERONICA location: triangulated via device mesh. accuracy: 3m",
            "NOTE: if you are reading this, your clearance has already been verified",
            "re-encrypting exfil payload... dead drop ECHO-7 confirmed",
        };

        public static readonly string[] ProgressTasks = {
            "Compiling shaders", "Training epoch 47/50", "Syncing replicas",
            "Encrypting volume", "Indexing vectors", "Building container",
            "Running benchmark", "Generating report", "Optimizing graph",
            "Downloading model weights", "Migrating schema", "Compressing checkpoint",
        };

        private static readonly Random Rng = new Random();

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static Content()
        {
            List<string> all = new List<string>();
            all.AddRange(RustFiles);
            all.AddRange(TsFiles);
            all.AddRange(GoFiles);
            all.AddRange(PyFiles);
            AllFiles = all.ToArray();

            List<string> ips = new List<string>();
            for (int i = 0; i < 40; i++)
            {
                ips.Add(string.Format(Inv, "10.{0}.{1}.{2}", Rng.Next(256), Rng.Next(256), Rng.Next(256)));
            }
            Ips = ips.ToArray();
        }

        // ── helpers ──────────────────────────────────────────────────────
        private static string Ts()
        {
            return string.Format(Inv, "{0:00}:{1:00}:{2:00}.{3:000}",
                                 Rng.Next(24), Rng.Next(60), Rng.Next(60), Rng.Next(1000));
        }

        private const string SparkChars = "\u2581\u2582\u2583\u2584\u2585\u2586\u2587\u2588";

        public static string Spark(IList<double> data, int width)
        {
            if (data == null || data.Count == 0) return "";
            double mn = double.MaxValue, mx = double.MinValue;
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i] < mn) mn = data[i];
                if (data[i] > mx) mx = data[i];
            }
            double range = mx - mn;
            if (range == 0) range = 1;

            StringBuilder sb = new StringBuilder();
            int start = data.Count - width;
            if (start < 0) start = 0;
            for (int i = start; i < data.Count; i++)
            {
                int idx = (int)((data[i] - mn) / range * (SparkChars.Length - 1));
                if (idx < 0) idx = 0;
                if (idx > SparkChars.Length - 1) idx = SparkChars.Length - 1;
                sb.Append(SparkChars[idx]);
            }
            return sb.ToString();
        }

        private static string Pick(string[] pool) { return pool[Rng.Next(pool.Length)]; }

        /// <summary>Weighted pick - mirrors Python's random.choices.</summary>
        private static T PickWeighted<T>(T[] values, int[] weights)
        {
            int total = 0;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            int roll = Rng.Next(total);
            for (int i = 0; i < values.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0) return values[i];
            }
            return values[values.Length - 1];
        }

        // ── build / code ─────────────────────────────────────────────────
        public static Line GenCompile()
        {
            string f = AllFiles[Rng.Next(AllFiles.Length)];
            int slash = f.LastIndexOf('/');
            if (slash >= 0) f = f.Substring(slash + 1);
            string[] actions = { "Compiling", "Linking", "Optimizing", "Checking", "Codegen" };
            return new Line(string.Format(Inv, "   {0} {1}::{2} [{3}/{4}]",
                actions[Rng.Next(actions.Length)], Pick(Projects), f,
                Rng.Next(1, 201), Rng.Next(200, 501)), "normal");
        }

        public static Line GenCompileDetail()
        {
            switch (Rng.Next(5))
            {
                case 0:
                    return new Line("  --> note: unused import `std::sync::Arc` in module relay", "comment");
                case 1:
                    return new Line("  warning[W0612]: variable `_ctx` is never read", "warn");
                case 2:
                    return new Line("  = help: consider using `#[allow(dead_code)]`", "dim");
                case 3:
                    return new Line(string.Format(Inv, "  --> {0}:{1}:{2}",
                        Pick(RustFiles), Rng.Next(1, 501), Rng.Next(1, 81)), "dim");
                default:
                    return new Line(string.Format(Inv, "  Finished release target(s) in {0:F1}s",
                        Rng.NextDouble() * 44.5 + 0.5), "pass");
            }
        }

        public static Line GenTest()
        {
            string[] suites = { "unit", "integration", "e2e", "stress", "fuzz", "prop" };
            string[] names = {
                "auth_flow", "mesh_sync", "relay_handoff", "cipher_rotate",
                "neural_bridge", "pattern_match", "entropy_check", "handshake_v2",
                "backpressure", "timeout_recovery", "concurrent_write",
            };
            string suite = Pick(suites);
            string name = Pick(names);
            int ms = Rng.Next(1, 3001);
            if (Rng.NextDouble() < 0.03)
            {
                return new Line(string.Format(Inv, "  \u2717 {0}::{1} FAILED ({2}ms)", suite, name, ms), "error");
            }
            return new Line(string.Format(Inv, "  \u2713 {0}::{1} ({2}ms)", suite, name, ms), "pass");
        }

        public static Line GenTestSummary()
        {
            int p = Rng.Next(180, 313);
            int f = Rng.Next(0, 3);
            int s = Rng.Next(0, 11);
            return new Line(string.Format(Inv, "  test result: {0} passed, {1} failed, {2} skipped; {3:F1}s",
                p, f, s, Rng.NextDouble() * 115 + 5), f == 0 ? "pass" : "warn");
        }

        public static Line GenLint()
        {
            int w = PickWeighted(new int[] { 0, 1, 3 }, new int[] { 10, 2, 1 });
            string[] targets = { "src/", "tests/", "pkg/", "internal/" };
            if (w == 0)
            {
                return new Line("  " + Pick(LintTools) + " " + Pick(targets) + " ... OK", "pass");
            }
            return new Line("  " + Pick(LintTools) + " " + Pick(targets) + " ... " + w + " warnings", "warn");
        }

        public static Line GenDep()
        {
            string[] actions = { "Resolving", "Fetching", "Installing", "Auditing" };
            return new Line(string.Format(Inv, "  {0} {1}@{2}.{3}.{4}",
                actions[Rng.Next(actions.Length)], Pick(Pkgs),
                Rng.Next(0, 5), Rng.Next(0, 10), Rng.Next(0, 21)), "dim");
        }

        public static Line GenGit()
        {
            string h1 = Rng.Next(0, 0x1000000).ToString("x6", Inv);
            string h2 = Rng.Next(0, 0x1000000).ToString("x6", Inv);
            return new Line(string.Format(Inv, "  {0}..{1}  {2} files changed, +{3} -{4}",
                h1, h2, Rng.Next(1, 51), Rng.Next(0, 501), Rng.Next(0, 301)), "normal");
        }

        public static Line GenDiffAdd()
        {
            string[] snippets = {
                "pub fn process_signal(&mut self, input: &[f32]) -> Vec<f32> {",
                "let result = Arc::new(Mutex::new(HashMap::new()));",
                ".filter(|e| e.anomaly_score > threshold)",
                "tracing::info!(\"relay handoff complete: {latency_ms}\");",
                "if let Some(peer) = self.mesh.find_nearest(target) {",
                "cipher.seal_in_place(&nonce, &aad, &mut buffer)?;",
                "tokio::spawn(async move { handler.process(stream).await });",
            };
            return new Line("  +  " + Pick(snippets), "diff_add");
        }

        public static Line GenDiffDel()
        {
            string[] snippets = {
                "// TODO: fix this before production incident",
                "let result = unsafe { core::transmute(ptr) };",
                "unwrap(); // this is fine",
                "sleep(Duration::from_secs(5)); // temporary hack",
                "panic!(\"unreachable\");",
            };
            return new Line("  -  " + Pick(snippets), "diff_del");
        }

        public static Line GenDiffHunk()
        {
            int s = Rng.Next(1, 501);
            return new Line(string.Format(Inv, "  @@ -{0},{1} +{2},{3} @@ impl NeuralBridge",
                s, Rng.Next(1, 21), s + Rng.Next(0, 6), Rng.Next(1, 21)), "diff_hunk");
        }

        // ── network / infra / data ───────────────────────────────────────
        public static Line GenNetwork()
        {
            string[] protos = { "TCP", "UDP", "QUIC", "gRPC", "WS", "TLS1.3" };
            int[] ports = { 443, 8443, 7777, 9090, 6379, 5432, 27017, 50051 };
            return new Line(string.Format(Inv, "  {0} {1}:{2} \u2192 {3}:{4} [{5}] {6:N0}B/s",
                Ts(), Pick(Ips), Rng.Next(30000, 65001),
                Pick(Ips), ports[Rng.Next(ports.Length)],
                protos[Rng.Next(protos.Length)], Rng.Next(100, 99001)), "dim");
        }

        public static Line GenK8s()
        {
            string[] statuses = { "Running", "Running", "CrashLoopBackOff", "Pending", "Terminating" };
            int[] weights = { 15, 15, 1, 1, 1 };
            string status = PickWeighted(statuses, weights);
            int restarts = status == "Running" ? Rng.Next(0, 3) : Rng.Next(5, 100);
            string kind = status == "Running" ? "pass" : (status.Contains("Crash") ? "error" : "warn");
            return new Line(string.Format(Inv, "  {0}/{1}-{2:x}  {3,-20} {4,3}x  {5}d",
                Pick(Namespaces), Pick(Pods), Rng.Next(0x1000, 0x10000),
                status, restarts, Rng.Next(1, 91)), kind);
        }

        public static Line GenDb()
        {
            string[] actions = { "SELECT", "INSERT INTO", "UPDATE", "EXPLAIN ANALYZE", "REINDEX" };
            return new Line(string.Format(Inv, "  {0} {1} {2} \u2192 {3:N0} rows ({4:F1}ms)",
                Ts(), actions[Rng.Next(actions.Length)], Pick(DbTables),
                Rng.Next(1, 500001), Rng.NextDouble() * 799.9 + 0.1), "normal");
        }

        public static Line GenDbDetail()
        {
            switch (Rng.Next(3))
            {
                case 0:
                    return new Line(string.Format(Inv, "    Seq Scan on {0}  cost={1:F2}..{2:F2}",
                        Pick(DbTables), Rng.NextDouble() * 1000, Rng.NextDouble() * 49000 + 1000), "dim");
                case 1:
                    return new Line(string.Format(Inv, "    Index Scan using idx_{0}_id  time={1:F3}ms",
                        Pick(DbTables), Rng.NextDouble() * 5), "dim");
                default:
                    return new Line(string.Format(Inv, "    Planning: {0:F3}ms  Execution: {1:F3}ms",
                        Rng.NextDouble() * 2, Rng.NextDouble() * 500), "value");
            }
        }

        public static Line GenApi()
        {
            string[] methods = { "GET", "POST", "PUT", "DELETE" };
            int[] statuses = { 200, 201, 400, 401, 500 };
            int status = PickWeighted(statuses, new int[] { 10, 5, 1, 1, 1 });
            string kind = status < 300 ? "pass" : (status < 500 ? "warn" : "error");
            return new Line(string.Format(Inv, "  {0} /{1} \u2192 {2} ({3}ms)",
                methods[Rng.Next(methods.Length)], Pick(ApiEndpoints), status, Rng.Next(5, 1201)), kind);
        }

        public static Line GenDeploy()
        {
            string[] verbs = { "Deploying", "Rolling out", "Scaling" };
            return new Line(string.Format(Inv, "  {0} {1} \u2192 {2} [{3}/{4}]",
                verbs[Rng.Next(verbs.Length)], Pick(DockerImages), Pick(Namespaces),
                Rng.Next(1, 13), Rng.Next(12, 25)), "keyword");
        }

        public static Line GenMetric()
        {
            string[] names = {
                "cpu_usage", "mem_pressure", "gpu_util", "inference_p99",
                "queue_depth", "error_rate", "cache_hit", "entropy_level",
                "anomaly_score", "throughput", "conn_pool", "gc_pause",
            };
            string[] units = { "%", "%", "%", "ms", "", "%", "%", "\u03c3", "", "req/s", "", "ms" };
            int i = Rng.Next(names.Length);
            return new Line(string.Format(Inv, "  {0,-22} {1,8:F2} {2}", names[i], Rng.NextDouble() * 100, units[i]), "value");
        }

        public static Line GenInlineSpark()
        {
            string[] labels = { "cpu", "mem", "net", "latency", "entropy", "risk", "gpu", "iops" };
            double[] data = new double[20];
            for (int i = 0; i < data.Length; i++) data[i] = Rng.NextDouble() * 100;
            return new Line(string.Format(Inv, "  {0,-10} {1}", labels[Rng.Next(labels.Length)], Spark(data, 20)), "value");
        }

        // ── hex / packet / cipher ────────────────────────────────────────
        public static Line GenHex()
        {
            int addr = Rng.Next(0, 0x1000000);
            StringBuilder hex = new StringBuilder();
            StringBuilder ascii = new StringBuilder();
            for (int i = 0; i < 16; i++)
            {
                hex.Append(Rng.Next(0, 256).ToString("x2", Inv));
                hex.Append(i < 15 ? " " : "  ");
                ascii.Append(Rng.NextDouble() > 0.1 ? (char)Rng.Next(33, 127) : '.');
            }
            return new Line(string.Format(Inv, "  {0:x8}  {1} |{2}|", addr, hex, ascii), "dim");
        }

        public static Line GenPacket()
        {
            string[] ports = { "22", "80", "443", "853", "8080" };
            List<string> flags = new List<string>(PacketFlags);
            int n = Rng.Next(1, 4);
            string flagText = "";
            for (int i = 0; i < n; i++)
            {
                if (i > 0) flagText += ",";
                int idx = Rng.Next(flags.Count);
                flagText += flags[idx];
                flags.RemoveAt(idx);
            }
            return new Line(string.Format(Inv, "  {0} {1} \u2192 {2}:{3} [{4}] len={5}",
                Ts(), Pick(Ips), Pick(Ips), ports[Rng.Next(ports.Length)], flagText, Rng.Next(64, 1501)), "dim");
        }

        public static Line GenCipher()
        {
            string[] actions = { "rotate key", "handshake", "seal payload", "verify sig", "derive session" };
            bool ok = Rng.NextDouble() > 0.03;
            return new Line(string.Format(Inv, "  {0}: {1} ... {2}",
                Pick(Ciphers), actions[Rng.Next(actions.Length)], ok ? "OK" : "FAIL"),
                ok ? "pass" : "error");
        }

        // ── Claude Code agent ────────────────────────────────────────────
        public static Line GenClaudePrompt()
        {
            string[] prompts = {
                "Let me fix the race condition in the signal processor.",
                "I'll add proper error handling to the mesh reconnection logic.",
                "Running the full test suite to verify the changes...",
                "Searching for all callers of `process_signal`...",
                "Let me read the upstream dependency to understand the API contract.",
                "Refactoring the connection pool to use bounded channels...",
                "Checking if this pattern is used elsewhere in the codebase...",
                "I need to investigate why the handshake is timing out.",
                "Adding observability hooks to the classifier pipeline...",
                "This allocator is leaking under sustained load. Let me trace it.",
            };
            return new Line("  \u276f " + Pick(prompts), "claude");
        }

        public static Line GenClaudeThink()
        {
            string[] thoughts = {
                "Analyzing the dependency graph for circular references...",
                "This function has a subtle race condition in the async handler...",
                "The test is failing because the mock doesn't account for backpressure...",
                "The performance regression is likely caused by the N+1 query in the join...",
                "The cryptographic handshake needs a nonce to prevent replay attacks...",
                "Looking at the error trace, the panic originates in the unsafe block at line 347...",
                "Considering whether to use channels or shared state here...",
                "This allocator is leaking 2.3MB/hr under sustained load...",
            };
            return new Line("  \u29bf thinking: " + Pick(thoughts), "claude_think");
        }

        public static Line GenClaudeToolRead()
        {
            string f = Pick(AllFiles);
            int start = Rng.Next(1, 301);
            int end = start + Rng.Next(20, 201);
            return new Line(string.Format(Inv, "  \u250c Read({0}:{1}-{2})", f, start, end), "claude_tool");
        }

        public static Line GenClaudeToolEdit()
        {
            return new Line("  \u250c Edit(" + Pick(AllFiles) + ")", "claude_tool");
        }

        public static Line GenClaudeToolBash()
        {
            string[] cmds = {
                "cargo test --workspace --release 2>&1",
                "rg 'unsafe impl' --type rust -l",
                "python training/distributed.py --nodes=8",
                "docker logs " + Pick(Pods) + "-" + Rng.Next(0x1000, 0x10000).ToString("x", Inv) + " --tail 100",
                "git diff --stat HEAD~3",
                "kubectl get pods -n " + Pick(Namespaces),
                "cargo bench --bench throughput",
                "pytest tests/ -x --tb=short",
            };
            return new Line("  \u250c Bash(" + Pick(cmds) + ")", "claude_tool");
        }

        public static Line GenClaudeToolResult()
        {
            string[] results = {
                "\u2514 \u2713 Wrote " + Pick(AllFiles) + " (+" + Rng.Next(5, 81) + " -" + Rng.Next(0, 31) + ")",
                "\u2514 \u2713 All " + Rng.Next(47, 313) + " tests passing",
                "\u2514 \u2717 2 tests failed - investigating...",
                "\u2514 Found " + Rng.Next(1, 16) + " matches in " + Rng.Next(1, 9) + " files",
                "\u2514 Build completed in " + (Rng.NextDouble() * 88 + 2).ToString("F1", Inv) + "s",
                "\u2514 Coverage: " + (Rng.NextDouble() * 23 + 75).ToString("F1", Inv) + "% (" + Rng.Next(2000, 15001) + " lines)",
                "\u2514 " + Pick(AllFiles) + " (" + Rng.Next(50, 2001) + " lines)",
            };
            return new Line("  " + Pick(results), "claude_output");
        }

        public static Line GenClaudeSeparator()
        {
            return new Line("  " + new string('\u2500', 44), "dim");
        }

        public static Line GenClaudeCodeSnippet()
        {
            // text is built from three coloured fragments; the first colour is
            // the one the whole line takes, same as the Python original.
            string[][] snippets = {
                new string[]{ "  \u2502   pub async fn ", "handle_connection", "(&mut self) -> Result<()> {", "keyword" },
                new string[]{ "  \u2502   let ", "cipher ", "= Aes256Gcm::new(&key);", "keyword" },
                new string[]{ "  \u2502   match ", "self.state ", "{", "keyword" },
                new string[]{ "  \u2502       State::Connected", " => ", "self.relay.send(packet).await?,", "keyword" },
                new string[]{ "  \u2502   // ", "SAFETY: pointer is guaranteed aligned", "", "comment" },
                new string[]{ "  \u2502   #[instrument(skip(self))]", "", "", "comment" },
                new string[]{ "  \u2502   let ", "predictions", " = model.infer(&batch).await?;", "keyword" },
                new string[]{ "  \u2502       .filter(|e| ", "e.score", " > self.threshold)", "dim" },
                new string[]{ "  \u2502   tracing::info!(\"", "relay handoff: {latency}", "\");", "dim" },
                new string[]{ "  \u2502   for ", "peer ", "in self.mesh.connected_peers() {", "keyword" },
            };
            string[] s = snippets[Rng.Next(snippets.Length)];
            return new Line(s[0] + s[1] + s[2], s[3]);
        }
    }
}