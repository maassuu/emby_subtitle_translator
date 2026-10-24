#!/usr/bin/env sh
# Builds the plugin and puts NapisyAi.dll in dist/.
set -e
cd "$(dirname "$0")"
dotnet test
dotnet build src/NapisyAi -c Release
mkdir -p dist
cp src/NapisyAi/bin/Release/netstandard2.0/NapisyAi.dll dist/
echo "Gotowe / Done: dist/NapisyAi.dll"
