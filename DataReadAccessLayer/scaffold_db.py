import json
import shutil
import subprocess
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
SOLUTION_DIR = SCRIPT_DIR.parent
APPSETTINGS = SOLUTION_DIR / "WebApi" / "appsettings.json"

PROJECT_NAME = "DataReadAccessLayer"
CONTEXT_NAME = "ClubReadDbContext"
CONTEXT_FILE = f"{CONTEXT_NAME}.cs"
OUTPUT_FOLDER = "Models"

TABLES = [
    "rooms",
    "classes",
    "enrollments",
    "passes",
    "waitlist_entries",
]


def get_connection_string() -> str:
    if not APPSETTINGS.exists():
        sys.exit(f"Не найден файл: {APPSETTINGS}")

    data = json.loads(APPSETTINGS.read_text(encoding="utf-8"))
    connection_string = data.get("ConnectionStrings", {}).get("Postgres")
    if not connection_string:
        sys.exit("В appsettings.json не найден ConnectionStrings:Postgres")
    return connection_string


def clean_previous_scaffold():
    context_file = SCRIPT_DIR / CONTEXT_FILE
    if context_file.exists():
        print(f"Удаляю: {context_file.name}")
        context_file.unlink()

    models_dir = SCRIPT_DIR / OUTPUT_FOLDER
    if models_dir.exists():
        print(f"Удаляю папку: {models_dir.name}/")
        shutil.rmtree(models_dir)


def main():
    connection_string = get_connection_string()

    subprocess.run(["dotnet", "tool", "restore"], cwd=SOLUTION_DIR, check=True)
    subprocess.run(["dotnet", "build", PROJECT_NAME], cwd=SOLUTION_DIR, check=True)

    clean_previous_scaffold()

    cmd = [
        "dotnet", "dotnet-ef", "dbcontext", "scaffold",
        connection_string,
        "Npgsql.EntityFrameworkCore.PostgreSQL",
        "--project", str(SCRIPT_DIR),
        "--startup-project", str(SCRIPT_DIR),
        "--namespace", f"{PROJECT_NAME}.Models",
        "--context-namespace", PROJECT_NAME,
        "--context", CONTEXT_NAME,
        "--output-dir", OUTPUT_FOLDER,
        "--context-dir", ".",
    ]
    for table in TABLES:
        cmd += ["--table", table]
    cmd += [
        "--no-onconfiguring",
        "--force",
        "--no-build",
    ]

    print(f"Scaffold из: {connection_string}")
    result = subprocess.run(cmd, cwd=SOLUTION_DIR)
    sys.exit(result.returncode)


if __name__ == "__main__":
    main()
