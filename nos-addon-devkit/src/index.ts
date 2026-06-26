import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";
import { spawnSync } from "child_process";
import * as fs from "fs";
import * as os from "os";
import * as path from "path";
import { fileURLToPath } from "url";

// ---------------------------------------------------------------------------
// Path resolution
// ---------------------------------------------------------------------------

// dist/index.js → plugin root (one level up)
const PLUGIN_ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));

// NoSAddonCompiler is bundled inside the plugin
const COMPILER_PROJECT = process.env.NOS_COMPILER_PROJECT?.trim() ||
  path.join(PLUGIN_ROOT, "NoSAddonCompiler");

// ---------------------------------------------------------------------------
// Config file: ~/.claude/nos-addon-devkit.json
// Stores overrides set via nos_set_game_dir (takes priority over env vars)
// ---------------------------------------------------------------------------

const CONFIG_PATH = path.join(os.homedir(), ".claude", "nos-addon-devkit.json");

interface DevKitConfig {
  game_dir?: string;
}

function readConfig(): DevKitConfig {
  try {
    return JSON.parse(fs.readFileSync(CONFIG_PATH, "utf8")) as DevKitConfig;
  } catch {
    return {};
  }
}

function writeConfig(config: DevKitConfig): void {
  const dir = path.dirname(CONFIG_PATH);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(CONFIG_PATH, JSON.stringify(config, null, 2), "utf8");
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

interface Diagnostic {
  file: string;
  line: number;
  col: number;
  severity: "error" | "warning";
  code: string;
  message: string;
}

function parseDiagnostics(output: string): Diagnostic[] {
  const diagnostics: Diagnostic[] = [];
  const pattern = /^(.+?)\((\d+),(\d+)\): (error|warning) (CS\w+): (.+)$/gm;
  let m: RegExpExecArray | null;
  while ((m = pattern.exec(output)) !== null) {
    diagnostics.push({
      file: m[1],
      line: parseInt(m[2], 10),
      col: parseInt(m[3], 10),
      severity: m[4] as "error" | "warning",
      code: m[5],
      message: m[6],
    });
  }
  return diagnostics;
}

// ---------------------------------------------------------------------------
// Server
// ---------------------------------------------------------------------------

const server = new Server(
  { name: "nos-addon-devkit", version: "1.0.0" },
  { capabilities: { tools: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: "nos_set_game_dir",
      description:
        "Save the Among Us game directory path to the DevKit config file. " +
        "This overrides the path set at plugin install time. " +
        "The path must be the game root directory containing 'Among Us.exe' with NoS installed.",
      inputSchema: {
        type: "object" as const,
        properties: {
          game_dir: {
            type: "string",
            description: "Absolute path to the Among Us game directory (e.g. 'C:\\\\Games\\\\Among Us').",
          },
        },
        required: ["game_dir"],
      },
    },
    {
      name: "nos_check",
      description:
        "Check NoS addon scripts for errors and warnings." +
        "Use this to validate scripts during development — it reports syntax errors, type errors, " +
        "and missing references just like the in-game compiler would see them. " +
        "Uses the game directory from plugin settings or nos_set_game_dir. ",
      inputSchema: {
        type: "object" as const,
        properties: {
          addon_dir: {
            type: "string",
            description:
              "Absolute path to the addon root directory — the one that directly contains addon.meta " +
              "(and the Scripts/ subfolder). UseHiddenMembers is read automatically from Scripts/.behaviour.",
          },
        },
        required: ["addon_dir"],
      },
    },
  ],
}));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  if (name === "nos_set_game_dir") {
    const { game_dir } = args as { game_dir: string };

    if (!fs.existsSync(game_dir)) {
      return {
        content: [{
          type: "text" as const,
          text: `Error: directory not found: ${game_dir}`,
        }],
      };
    }

    const exePath = path.join(game_dir, "Among Us.exe");
    const warning = !fs.existsSync(exePath)
      ? `\nWarning: "Among Us.exe" not found in ${game_dir}. Please verify the path.`
      : "";

    const config = readConfig();
    config.game_dir = game_dir;
    writeConfig(config);

    return {
      content: [{
        type: "text" as const,
        text: `Game directory saved: ${game_dir}\nConfig written to: ${CONFIG_PATH}${warning}`,
      }],
    };
  }

  if (name === "nos_check") {
    const { addon_dir } = args as { addon_dir: string };

    const config = readConfig();
    const game_dir = config.game_dir || process.env.NOS_GAME_DIR?.trim();

    if (!game_dir) {
      return {
        content: [{
          type: "text" as const,
          text: "Game directory is not configured.\n" +
            "Set it via plugin settings (reinstall/reconfigure) or run /nos-set-game-dir.",
        }],
      };
    }

    const dotnetArgs = [
      "run",
      "--project", COMPILER_PROJECT,
      "--",
      "--addon-dir", addon_dir,
      "--game-dir", game_dir,
    ];

    const proc = spawnSync("dotnet", dotnetArgs, {
      encoding: "utf8",
      timeout: 90_000,
    });

    const combined = (proc.stdout ?? "") + (proc.stderr ?? "");
    const success = proc.status === 0;
    const diagnostics = parseDiagnostics(combined);
    const errors = diagnostics.filter((d) => d.severity === "error");
    const warnings = diagnostics.filter((d) => d.severity === "warning");
    const summary = success
      ? `No errors. ${warnings.length} warning(s).`
      : `Check FAILED. ${errors.length} error(s), ${warnings.length} warning(s).`;

    return {
      content: [{
        type: "text" as const,
        text: JSON.stringify({ success, summary, diagnostics, raw_output: combined }, null, 2),
      }],
    };
  }

  throw new Error(`Unknown tool: ${name}`);
});

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main().catch((err) => {
  process.stderr.write(`Fatal: ${err}\n`);
  process.exit(1);
});
