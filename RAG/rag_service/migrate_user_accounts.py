#!/usr/bin/env python3
"""
User Account Migration Script

This script helps migrate existing user accounts to use the enhanced authentication system
that maps Unity user IDs to consistent user identifiers based on email addresses.
"""

import asyncio
import os
import psycopg
from typing import List, Dict, Any
import logging

# Setup logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class UserAccountMigrator:
    """Migrates user accounts to use enhanced authentication"""
    
    def __init__(self, db_url: str):
        self.db_url = db_url
    
    async def migrate_existing_accounts(self):
        """Migrate existing user accounts to use email-based mapping"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                # Get all existing user accounts
                await cur.execute("""
                    SELECT user_id, username, email, created_at, last_active
                    FROM user_accounts 
                    WHERE email IS NOT NULL AND email != ''
                    ORDER BY created_at ASC
                """)
                
                existing_accounts = await cur.fetchall()
                logger.info(f"Found {len(existing_accounts)} existing user accounts with emails")
                
                migrated_count = 0
                skipped_count = 0
                
                for account in existing_accounts:
                    user_id, username, email, created_at, last_active = account
                    
                    try:
                        # Check if this email already has a mapping
                        await cur.execute("""
                            SELECT primary_user_id FROM user_mapping 
                            WHERE email = %s AND is_active = TRUE
                            LIMIT 1
                        """, (email.lower().strip(),))
                        
                        existing_mapping = await cur.fetchone()
                        
                        if existing_mapping:
                            # Email already has a mapping, skip
                            logger.info(f"Skipping {email} - already has mapping to {existing_mapping[0]}")
                            skipped_count += 1
                            continue
                        
                        # Create email hash
                        import hashlib
                        email_hash = hashlib.sha256(email.lower().strip().encode('utf-8')).hexdigest()
                        
                        # Create primary user ID (use existing user_id as primary)
                        primary_user_id = user_id
                        
                        # Create mapping for this user
                        await cur.execute("""
                            INSERT INTO user_mapping (
                                email_hash, email, primary_user_id, unity_user_id, 
                                device_id, platform, created_at, last_seen, is_active
                            ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, TRUE)
                        """, (
                            email_hash,
                            email.lower().strip(),
                            primary_user_id,
                            user_id,  # Use existing user_id as unity_user_id for migration
                            'migrated',
                            'migration',
                            created_at,
                            last_active or created_at
                        ))
                        
                        migrated_count += 1
                        logger.info(f"Migrated {email} -> {primary_user_id}")
                        
                    except Exception as e:
                        logger.error(f"Error migrating account {email}: {e}")
                        continue
                
                await conn.commit()
                
                logger.info(f"Migration complete: {migrated_count} migrated, {skipped_count} skipped")
                return migrated_count, skipped_count
    
    async def create_missing_user_accounts(self):
        """Create user accounts for mappings that don't have corresponding user_accounts entries"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                # Find mappings without corresponding user accounts
                await cur.execute("""
                    SELECT DISTINCT um.primary_user_id, um.email, um.created_at
                    FROM user_mapping um
                    LEFT JOIN user_accounts ua ON um.primary_user_id = ua.user_id
                    WHERE ua.user_id IS NULL AND um.is_active = TRUE
                """)
                
                missing_accounts = await cur.fetchall()
                logger.info(f"Found {len(missing_accounts)} mappings without user accounts")
                
                created_count = 0
                
                for mapping in missing_accounts:
                    primary_user_id, email, created_at = mapping
                    
                    try:
                        # Create user account
                        username = f"User_{primary_user_id[:8]}"
                        
                        await cur.execute("""
                            INSERT INTO user_accounts (
                                user_id, account_type, username, email, max_prompts, 
                                energy_tokens, prompts_used, created_at, last_active, status
                            ) VALUES (%s, %s, %s, %s, %s, %s, 0, %s, %s, 'active')
                        """, (
                            primary_user_id,
                            'user',
                            username,
                            email,
                            100,  # Default max prompts
                            1000,  # Default energy tokens
                            created_at,
                            created_at
                        ))
                        
                        created_count += 1
                        logger.info(f"Created user account for {email} -> {primary_user_id}")
                        
                    except Exception as e:
                        logger.error(f"Error creating user account for {email}: {e}")
                        continue
                
                await conn.commit()
                logger.info(f"Created {created_count} missing user accounts")
                return created_count
    
    async def cleanup_duplicate_mappings(self):
        """Clean up duplicate mappings for the same email"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                # Find duplicate mappings
                await cur.execute("""
                    SELECT email, COUNT(*) as count
                    FROM user_mapping 
                    WHERE is_active = TRUE
                    GROUP BY email
                    HAVING COUNT(*) > 1
                """)
                
                duplicates = await cur.fetchall()
                logger.info(f"Found {len(duplicates)} emails with duplicate mappings")
                
                cleaned_count = 0
                
                for email, count in duplicates:
                    # Keep the oldest mapping, deactivate others
                    await cur.execute("""
                        WITH ranked_mappings AS (
                            SELECT id, ROW_NUMBER() OVER (ORDER BY created_at ASC) as rn
                            FROM user_mapping 
                            WHERE email = %s AND is_active = TRUE
                        )
                        UPDATE user_mapping 
                        SET is_active = FALSE 
                        WHERE id IN (
                            SELECT id FROM ranked_mappings WHERE rn > 1
                        )
                    """, (email,))
                    
                    affected = cur.rowcount
                    cleaned_count += affected
                    logger.info(f"Cleaned up {affected} duplicate mappings for {email}")
                
                await conn.commit()
                logger.info(f"Cleaned up {cleaned_count} duplicate mappings")
                return cleaned_count
    
    async def generate_migration_report(self):
        """Generate a report of the migration status"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                # Total user accounts
                await cur.execute("SELECT COUNT(*) FROM user_accounts")
                total_accounts = (await cur.fetchone())[0]
                
                # Total mappings
                await cur.execute("SELECT COUNT(*) FROM user_mapping WHERE is_active = TRUE")
                total_mappings = (await cur.fetchone())[0]
                
                # Unique emails
                await cur.execute("SELECT COUNT(DISTINCT email) FROM user_mapping WHERE is_active = TRUE")
                unique_emails = (await cur.fetchone())[0]
                
                # Accounts with mappings
                await cur.execute("""
                    SELECT COUNT(DISTINCT ua.user_id) 
                    FROM user_accounts ua
                    INNER JOIN user_mapping um ON ua.user_id = um.primary_user_id
                    WHERE um.is_active = TRUE
                """)
                accounts_with_mappings = (await cur.fetchone())[0]
                
                # Mappings without accounts
                await cur.execute("""
                    SELECT COUNT(DISTINCT um.primary_user_id)
                    FROM user_mapping um
                    LEFT JOIN user_accounts ua ON um.primary_user_id = ua.user_id
                    WHERE ua.user_id IS NULL AND um.is_active = TRUE
                """)
                mappings_without_accounts = (await cur.fetchone())[0]
                
                report = {
                    "total_user_accounts": total_accounts,
                    "total_mappings": total_mappings,
                    "unique_emails": unique_emails,
                    "accounts_with_mappings": accounts_with_mappings,
                    "mappings_without_accounts": mappings_without_accounts,
                    "migration_completeness": (accounts_with_mappings / total_accounts * 100) if total_accounts > 0 else 0
                }
                
                logger.info("=== Migration Report ===")
                logger.info(f"Total user accounts: {report['total_user_accounts']}")
                logger.info(f"Total active mappings: {report['total_mappings']}")
                logger.info(f"Unique emails: {report['unique_emails']}")
                logger.info(f"Accounts with mappings: {report['accounts_with_mappings']}")
                logger.info(f"Mappings without accounts: {report['mappings_without_accounts']}")
                logger.info(f"Migration completeness: {report['migration_completeness']:.1f}%")
                
                return report

async def main():
    """Main migration function"""
    database_url = os.getenv("DATABASE_URL", "postgresql://user:password@localhost/rag_db")
    
    migrator = UserAccountMigrator(database_url)
    
    logger.info("Starting user account migration...")
    
    try:
        # Step 1: Migrate existing accounts
        logger.info("Step 1: Migrating existing user accounts...")
        migrated, skipped = await migrator.migrate_existing_accounts()
        
        # Step 2: Create missing user accounts
        logger.info("Step 2: Creating missing user accounts...")
        created = await migrator.create_missing_user_accounts()
        
        # Step 3: Clean up duplicates
        logger.info("Step 3: Cleaning up duplicate mappings...")
        cleaned = await migrator.cleanup_duplicate_mappings()
        
        # Step 4: Generate report
        logger.info("Step 4: Generating migration report...")
        report = await migrator.generate_migration_report()
        
        logger.info("Migration completed successfully!")
        
    except Exception as e:
        logger.error(f"Migration failed: {e}")
        raise

if __name__ == "__main__":
    asyncio.run(main())
