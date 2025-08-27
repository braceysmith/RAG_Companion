#!/usr/bin/env python3
"""
Quick syntax check for rag_api.py
"""

import ast
import sys

def check_syntax(filename):
    """Check if a Python file has valid syntax"""
    try:
        with open(filename, 'r', encoding='utf-8') as f:
            source = f.read()
        
        # Parse the AST to check syntax
        ast.parse(source)
        print(f"✅ {filename} has valid Python syntax")
        return True
        
    except SyntaxError as e:
        print(f"❌ Syntax error in {filename}:")
        print(f"   Line {e.lineno}: {e.text}")
        print(f"   Error: {e.msg}")
        return False
        
    except Exception as e:
        print(f"❌ Error reading {filename}: {e}")
        return False

def main():
    """Check syntax of rag_api.py"""
    filename = "rag_api.py"
    
    print("🔍 Checking Python syntax...")
    print("=" * 40)
    
    if check_syntax(filename):
        print("\n🎉 Syntax check passed! The file should load without errors.")
        print("\n💡 Next steps:")
        print("   1. Try starting the RAG server again")
        print("   2. Check for any runtime errors")
        print("   3. Test the health endpoint")
    else:
        print("\n❌ Syntax check failed! Please fix the errors above.")
        sys.exit(1)

if __name__ == "__main__":
    main()
