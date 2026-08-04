#!/bin/sh
# Type-checks both .NET Framework projects without Windows and without Visual
# Studio: any machine with the .NET SDK will do (`brew install dotnet-sdk`).
#
# This is a compile check, not the build — see build/typecheck/*.csproj. It will
# not produce a deployable exe and it will not catch anything the real MSBuild
# build does differently (resources, manifests, packages.config resolution).
set -e
cd "$(dirname "$0")"

# The real projects list every source file; these shadow ones glob. Check the
# lists agree before compiling, or a new file passes here and fails on Windows.
node check_csproj.mjs

cd typecheck
for p in ModbusServer.typecheck.csproj TcpHMIClient.typecheck.csproj; do
    printf '\n==> %s\n' "$p"
    dotnet build "$p" --nologo -v:minimal "$@"
done
