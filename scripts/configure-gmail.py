#!/usr/bin/env python3
"""Configure development Gmail delivery without exposing credentials in arguments."""
import getpass
import json
from pathlib import Path
import subprocess
import sys


def main():
    if not sys.stdin.isatty():
        raise SystemExit("Run this command in an interactive terminal.")
    address = input("Sender Gmail address: ").strip()
    if "@" not in address or any(char.isspace() for char in address):
        raise SystemExit("Enter a valid sender email address.")
    password = getpass.getpass("Google app password (hidden): ").replace(" ", "")
    if len(password) != 16 or not password.isascii() or not password.isalpha():
        raise SystemExit("Expected the 16-letter Google app password, not your Google password.")
    project = Path(__file__).resolve().parents[1] / "backend/src/Nidhi.Api"
    result = subprocess.run(
        ["dotnet", "user-secrets", "set", "--project", str(project)],
        input=json.dumps({
            "IdentityEmail:Provider": "Gmail",
            "IdentityEmail:Gmail:Address": address,
            "IdentityEmail:Gmail:AppPassword": password,
            "IdentityEmail:PublicBaseUrl": "http://localhost:3000",
        }),
        text=True, capture_output=True,
    )
    if result.returncode:
        raise SystemExit("Configuration failed. Check that the .NET SDK is installed; no credentials were printed.")
    print("Gmail settings saved to local ASP.NET Core user-secrets. Restart the development API to apply them.")


if __name__ == "__main__":
    main()
