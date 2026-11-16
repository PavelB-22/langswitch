#!/bin/bash
# Проверка логики исправлений без Windows (нужен mono: apt install mono-devel)
set -e
cd "$(dirname "$0")/.."
mcs -nologo -langversion:latest -out:/tmp/lsw-test.exe -r:System.Windows.Forms.dll \
  test/WordTest.cs src/Dictionary.cs src/Corrector.cs src/LayoutConverter.cs src/Settings.cs
mono /tmp/lsw-test.exe res
