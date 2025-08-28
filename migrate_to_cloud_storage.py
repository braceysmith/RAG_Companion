#!/usr/bin/env python3
"""
Database Migration Script: Add Cloud Storage Support

This script adds cloud storage fields to existing multimedia_content tables.
Run this after updating your database.py schema.
"""

import os
import asyncio
import psycopg
from psycopg import sql

async def migrate_database():
    """Add cloud storage fields to existing database"""
    
    # Get database URL from environment
    database_url = os.getenv("DATABASE_URL")
    if not database_url:
        print("❌ DATABASE_URL environment variable not set")
        return False
    
    try:
        print("🔧 Starting database migration for cloud storage...")
        
        async with await psycopg.AsyncConnection.connect(database_url) as conn:
            async with conn.cursor() as cur:
                
                # Check if columns already exist
                await cur.execute("""
                    SELECT column_name 
                    FROM information_schema.columns 
                    WHERE table_name = 'multimedia_content' 
                    AND column_name IN ('cloud_url', 'cloud_public_id')
                """)
                
                existing_columns = [row[0] for row in await cur.fetchall()]
                
                # Add cloud_url column if it doesn't exist
                if 'cloud_url' not in existing_columns:
                    print("➕ Adding cloud_url column...")
                    await cur.execute("""
                        ALTER TABLE multimedia_content 
                        ADD COLUMN cloud_url TEXT
                    """)
                    print("✅ Added cloud_url column")
                else:
                    print("✅ cloud_url column already exists")
                
                # Add cloud_public_id column if it doesn't exist
                if 'cloud_public_id' not in existing_columns:
                    print("➕ Adding cloud_public_id column...")
                    await cur.execute("""
                        ALTER TABLE multimedia_content 
                        ADD COLUMN cloud_public_id TEXT
                    """)
                    print("✅ Added cloud_public_id column")
                else:
                    print("✅ cloud_public_id column already exists")
                
                # Make file_path nullable (since cloud storage might not need it)
                await cur.execute("""
                    ALTER TABLE multimedia_content 
                    ALTER COLUMN file_path DROP NOT NULL
                """)
                print("✅ Made file_path nullable")
                
                await conn.commit()
                print("🎉 Database migration completed successfully!")
                return True
                
    except Exception as e:
        print(f"❌ Migration failed: {e}")
        return False

async def verify_migration():
    """Verify that the migration was successful"""
    
    database_url = os.getenv("DATABASE_URL")
    if not database_url:
        print("❌ DATABASE_URL environment variable not set")
        return False
    
    try:
        async with await psycopg.AsyncConnection.connect(database_url) as conn:
            async with conn.cursor() as cur:
                
                # Check table structure
                await cur.execute("""
                    SELECT column_name, data_type, is_nullable
                    FROM information_schema.columns 
                    WHERE table_name = 'multimedia_content'
                    ORDER BY ordinal_position
                """)
                
                columns = await cur.fetchall()
                print("\n📊 Current multimedia_content table structure:")
                print("-" * 50)
                
                for col_name, data_type, is_nullable in columns:
                    nullable_str = "NULL" if is_nullable == "YES" else "NOT NULL"
                    print(f"  {col_name:<20} {data_type:<15} {nullable_str}")
                
                # Check for cloud storage columns
                cloud_columns = [col[0] for col in columns if 'cloud' in col[0].lower()]
                if cloud_columns:
                    print(f"\n✅ Cloud storage columns found: {', '.join(cloud_columns)}")
                else:
                    print("\n❌ No cloud storage columns found")
                
                return True
                
    except Exception as e:
        print(f"❌ Verification failed: {e}")
        return False

async def main():
    """Main migration function"""
    print("🚀 Cloud Storage Database Migration")
    print("=" * 50)
    
    # Run migration
    success = await migrate_database()
    if success:
        print("\n🔍 Verifying migration...")
        await verify_migration()
    else:
        print("❌ Migration failed, cannot verify")

if __name__ == "__main__":
    asyncio.run(main())
