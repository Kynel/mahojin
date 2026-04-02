#!/bin/bash
# Simple script to check if Unity code compiles
/opt/unity/Editor/Unity -quit -batchmode -nographics -projectPath . -executeMethod UnityEditor.SyncVS.SyncSolution
msbuild /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:WarningLevel=0 Assembly-CSharp.csproj
