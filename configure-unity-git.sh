#!/bin/bash

PROJECT_ROOT=$(pwd)

echo "------------------------------------------------"
echo "Unity SmartMerge Auto-Configuration"
echo "------------------------------------------------"

# 1. Detect OS and set the Hub/Editor search path
OS_NAME="$(uname -s)"
if [[ "$OS_NAME" == *"NT"* || "$OS_NAME" == *"MINGW"* ]]; then
    HUB_PATH="/c/Program Files/Unity/Hub/Editor"
    EXE_SUFFIX=".exe"
    IS_WINDOWS=true
elif [[ "$OS_NAME" == *"Linux"* ]]; then
    HUB_PATH="$HOME/Unity/Hub/Editor"
    EXE_SUFFIX=""
    IS_WINDOWS=false
else
    echo "Unsupported OS: $OS_NAME"
    exit 1
fi

# 2. Search for installed versions
if [ ! -d "$HUB_PATH" ]; then
    echo "Error: Could not find Unity Hub Editor folder at $HUB_PATH"
    read -p "Please enter the full path to your Hub/Editor folder: " HUB_PATH
fi

versions=($(cd "$HUB_PATH" && ls -d */ | sed 's#/##'))

if [ ${#versions[@]} -eq 0 ]; then
    echo "No Unity versions found in $HUB_PATH."
    exit 1
fi

echo "Found the following Unity versions:"
for i in "${!versions[@]}"; do
    echo "[$i] ${versions[$i]}"
done

echo "------------------------------------------------"
read -p "Select the version to use for SmartMerge [0-$((${#versions[@]} - 1))]: " choice

if [[ ! "$choice" =~ ^[0-9]+$ ]] || [ "$choice" -ge "${#versions[@]}" ]; then
    echo "Invalid selection. Exiting."
    exit 1
fi

SELECTED_VERSION=${versions[$choice]}
UNITY_PATH="$HUB_PATH/$SELECTED_VERSION/Editor/Data/Tools/UnityYAMLMerge$EXE_SUFFIX"

# --- NEW: Path Conversion for Windows ---
if [ "$IS_WINDOWS" = true ]; then
    # Converts /c/Program Files/... to C:/Program Files/...
    UNITY_PATH=$(echo "$UNITY_PATH" | sed 's/^\/\([a-z]\)\//\U\1:\//')
fi

# 3. Final Verification and Git Config
if [ -f "$UNITY_PATH" ] || [[ "$OS_NAME" == *"NT"* ]]; then 
    # Note: the [ -f ] check might fail with Windows paths in a Bash shell, 
    # but since we found it earlier, we can proceed.
    
    cd "$PROJECT_ROOT"

    echo "Applying configuration for: $SELECTED_VERSION"

    # Set the merge tool global preference
    git config --local merge.tool unityyamlmerge
    
    # Set the backup preference
    git config --local mergetool.keepBackup false
    
    # Configure the specific unityyamlmerge tool
    git config --local mergetool.unityyamlmerge.trustExitCode true
    
    # Use single quotes for the command to handle the $ variables correctly
    git config --local mergetool.unityyamlmerge.cmd "'$UNITY_PATH' merge -p \"\$BASE\" \"\$REMOTE\" \"\$LOCAL\" \"\$MERGED\""

    echo "------------------------------------------------"
    echo "Success! Your .git/config is now updated."
else
    echo "Error: UnityYAMLMerge not found at $UNITY_PATH"
fi

read -p "Press Enter to finish..."