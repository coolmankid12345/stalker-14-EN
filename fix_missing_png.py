#!/usr/bin/env python3
"""
Script to fix missing PNG references in RSI meta.json files.
Adds states for PNG files that exist but are not defined in metadata.
"""

import json
import os
from pathlib import Path
from glob import iglob

ALLOWED_RSI_DIR_GARBAGE = {
    "meta.json",
    ".DS_Store",
    "thumbs.db",
    ".directory"
}

def process_rsi(rsi_path: Path) -> bool:
    """Process a single RSI directory. Returns True if modified."""
    meta_path = rsi_path / "meta.json"
    
    if not meta_path.exists():
        return False
    
    try:
        with open(meta_path, 'r', encoding='utf-8-sig') as f:
            meta = json.load(f)
    except Exception as e:
        print(f"Error reading {meta_path}: {e}")
        return False
    
    # Get existing state names
    existing_states = {state["name"] for state in meta["states"]}
    
    # Find all PNG files in RSI directory
    png_files = set()
    for name in os.listdir(rsi_path):
        if name.endswith(".png"):
            png_name = name[:-4]  # Remove .png extension
            png_files.add(png_name)
    
    # Find missing states
    missing_states = png_files - existing_states
    
    if not missing_states:
        return False
    
    # Add missing states
    modified = False
    for state_name in sorted(missing_states):
        new_state = {"name": state_name}
        meta["states"].append(new_state)
        modified = True
        print(f"  + Added state: {state_name}")
    
    if modified:
        # Write back with proper formatting and trailing newline
        try:
            with open(meta_path, 'w', encoding='utf-8') as f:
                json.dump(meta, f, indent=2, ensure_ascii=False)
                f.write('\n')
        except Exception as e:
            print(f"Error writing {meta_path}: {e}")
            return False
    
    return modified


def main():
    base_dir = Path('Resources/Textures')
    processed = 0
    modified_count = 0

    for rsi_rel in iglob('**/*.rsi', root_dir=base_dir, recursive=True):
        rsi_path = base_dir / rsi_rel
        
        # Skip if no meta.json
        if not (rsi_path / "meta.json").exists():
            continue
            
        processed += 1
        if process_rsi(rsi_path):
            modified_count += 1

    print(f"\nProcessed {processed} RSI directories")
    print(f"Modified {modified_count} meta.json files")


if __name__ == '__main__':
    main()
