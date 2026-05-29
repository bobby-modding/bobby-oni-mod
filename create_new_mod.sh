#!/bin/bash

# 1. Check if a mod name was provided as an argument
if [ -z "$1" ]; then
  echo "Error: Please provide a mod name."
  echo "Usage: ./create_mod.sh <ModName>"
  exit 1
fi

MOD_NAME=$1

# 2. Check if a folder with this name already exists
if [ -d "$MOD_NAME" ]; then
  echo "Error: The directory '$MOD_NAME' already exists. Aborting to prevent overwriting."
  exit 1
fi

echo "Setting up new mod: $MOD_NAME..."

# 3. Execute the dotnet commands using the variable
dotnet new sln -n "$MOD_NAME" -o "$MOD_NAME" && \
dotnet new myonimodtemplate -n "$MOD_NAME" -o "$MOD_NAME" && \
dotnet sln "$MOD_NAME/$MOD_NAME.sln" add "$MOD_NAME/$MOD_NAME.csproj"

# 4. Confirm success
if [ $? -eq 0 ]; then
  echo "Success! '$MOD_NAME' is ready."
else
  echo "Failed: An error occurred during the dotnet commands."
fi