"""
Database operations for user needs assessment and tracking

This module handles storing and retrieving user needs data, goals, and progress tracking.
"""

import psycopg
from typing import Dict, List, Optional, Any
from datetime import datetime
import json
from .needs_framework import NeedCategory, NeedTier, NeedLevel

class NeedsDatabase:
    """Manages database operations for user needs and goals"""
    
    def __init__(self, database_url: str):
        self.database_url = database_url
    
    async def initialize_needs_tables(self):
        """Create necessary tables for needs tracking if they don't exist"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    # Create user_needs_assessments table
                    await cur.execute("""
                        CREATE TABLE IF NOT EXISTS user_needs_assessments (
                            id SERIAL PRIMARY KEY,
                            user_id TEXT NOT NULL,
                            assessment_date TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                            overall_score DECIMAL(5,2),
                            level_scores JSONB,
                            needs_status JSONB,
                            priority_needs JSONB,
                            created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                        )
                    """)
                    
                    # Create user_goals table
                    await cur.execute("""
                        CREATE TABLE IF NOT EXISTS user_goals (
                            id SERIAL PRIMARY KEY,
                            user_id TEXT NOT NULL,
                            goal_title TEXT NOT NULL,
                            goal_description TEXT,
                            goal_category TEXT,
                            goal_level INTEGER,
                            target_date DATE,
                            status TEXT DEFAULT 'active',
                            progress INTEGER DEFAULT 0,
                            created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                            updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                        )
                    """)
                    
                    # Create user_reminders table
                    await cur.execute("""
                        CREATE TABLE IF NOT EXISTS user_reminders (
                            id SERIAL PRIMARY KEY,
                            user_id TEXT NOT NULL,
                            reminder_text TEXT NOT NULL,
                            reminder_type TEXT,
                            due_date TIMESTAMP WITH TIME ZONE,
                            priority TEXT DEFAULT 'medium',
                            status TEXT DEFAULT 'pending',
                            needs_context JSONB,
                            created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                        )
                    """)
                    
                    # Create user_progress_logs table
                    await cur.execute("""
                        CREATE TABLE IF NOT EXISTS user_progress_logs (
                            id SERIAL PRIMARY KEY,
                            user_id TEXT NOT NULL,
                            category TEXT NOT NULL,
                            old_tier INTEGER,
                            new_tier INTEGER,
                            change_reason TEXT,
                            logged_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                        )
                    """)
                    
                    # Add indexes for better performance
                    await cur.execute("CREATE INDEX IF NOT EXISTS idx_user_needs_user_id ON user_needs_assessments(user_id)")
                    await cur.execute("CREATE INDEX IF NOT EXISTS idx_user_goals_user_id ON user_goals(user_id)")
                    await cur.execute("CREATE INDEX IF NOT EXISTS idx_user_reminders_user_id ON user_reminders(user_id)")
                    await cur.execute("CREATE INDEX IF NOT EXISTS idx_user_progress_user_id ON user_progress_logs(user_id)")
                    
                    await conn.commit()
                    return True
                    
        except Exception as e:
            print(f"❌ Failed to initialize needs tables: {str(e)}")
            return False
    
    async def store_needs_assessment(self, user_id: str, assessment: Dict) -> bool:
        """Store a user's needs assessment"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        INSERT INTO user_needs_assessments 
                        (user_id, overall_score, level_scores, needs_status, priority_needs)
                        VALUES (%s, %s, %s, %s, %s)
                    """, (
                        user_id,
                        assessment.get('overall_score', 0),
                        json.dumps(assessment.get('level_scores', {})),
                        json.dumps(assessment.get('needs_status', {})),
                        json.dumps(assessment.get('priority_needs', []))
                    ))
                    
                    await conn.commit()
                    return True
                    
        except Exception as e:
            print(f"❌ Failed to store needs assessment: {str(e)}")
            return False
    
    async def get_latest_needs_assessment(self, user_id: str) -> Optional[Dict]:
        """Get the most recent needs assessment for a user"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT overall_score, level_scores, needs_status, priority_needs, assessment_date
                        FROM user_needs_assessments 
                        WHERE user_id = %s 
                        ORDER BY assessment_date DESC 
                        LIMIT 1
                    """, (user_id,))
                    
                    row = await cur.fetchone()
                    if row:
                        return {
                            "overall_score": float(row[0]) if row[0] else 0,
                            "level_scores": json.loads(row[1]) if row[1] else {},
                            "needs_status": json.loads(row[2]) if row[2] else {},
                            "priority_needs": json.loads(row[3]) if row[3] else [],
                            "assessment_date": row[4].isoformat() if row[4] else None
                        }
                    return None
                    
        except Exception as e:
            print(f"❌ Failed to get needs assessment: {str(e)}")
            return None
    
    async def store_user_goal(self, user_id: str, goal_data: Dict) -> bool:
        """Store a user's goal"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        INSERT INTO user_goals 
                        (user_id, goal_title, goal_description, goal_category, goal_level, target_date, progress)
                        VALUES (%s, %s, %s, %s, %s, %s, %s)
                    """, (
                        user_id,
                        goal_data.get('title', ''),
                        goal_data.get('description', ''),
                        goal_data.get('category', ''),
                        goal_data.get('level', 1),
                        goal_data.get('target_date'),
                        goal_data.get('progress', 0)
                    ))
                    
                    await conn.commit()
                    return True
                    
        except Exception as e:
            print(f"❌ Failed to store user goal: {str(e)}")
            return False
    
    async def get_user_goals(self, user_id: str, status: str = 'active') -> List[Dict]:
        """Get all goals for a user"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT id, goal_title, goal_description, goal_category, goal_level, 
                               target_date, status, progress, created_at, updated_at
                        FROM user_goals 
                        WHERE user_id = %s AND status = %s
                        ORDER BY created_at DESC
                    """, (user_id, status))
                    
                    goals = []
                    async for row in cur:
                        goals.append({
                            "id": row[0],
                            "title": row[1],
                            "description": row[2],
                            "category": row[3],
                            "level": row[4],
                            "target_date": row[5].isoformat() if row[5] else None,
                            "status": row[6],
                            "progress": row[7],
                            "created_at": row[8].isoformat() if row[8] else None,
                            "updated_at": row[9].isoformat() if row[9] else None
                        })
                    
                    return goals
                    
        except Exception as e:
            print(f"❌ Failed to get user goals: {str(e)}")
            return []
    
    async def store_user_reminder(self, user_id: str, reminder_data: Dict) -> bool:
        """Store a user's reminder"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        INSERT INTO user_reminders 
                        (user_id, reminder_text, reminder_type, due_date, priority, needs_context)
                        VALUES (%s, %s, %s, %s, %s, %s)
                    """, (
                        user_id,
                        reminder_data.get('text', ''),
                        reminder_data.get('type', 'general'),
                        reminder_data.get('due_date'),
                        reminder_data.get('priority', 'medium'),
                        json.dumps(reminder_data.get('needs_context', {}))
                    ))
                    
                    await conn.commit()
                    return True
                    
        except Exception as e:
            print(f"❌ Failed to store user reminder: {str(e)}")
            return False
    
    async def get_user_reminders(self, user_id: str, status: str = 'pending') -> List[Dict]:
        """Get all reminders for a user"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT id, reminder_text, reminder_type, due_date, priority, 
                               needs_context, created_at
                        FROM user_reminders 
                        WHERE user_id = %s AND status = %s
                        ORDER BY due_date ASC
                    """, (user_id, status))
                    
                    reminders = []
                    async for row in cur:
                        reminders.append({
                            "id": row[0],
                            "text": row[1],
                            "type": row[2],
                            "due_date": row[3],  # Keep as datetime object
                            "priority": row[4],
                            "needs_context": json.loads(row[5]) if row[5] else {},
                            "created_at": row[6]  # Keep as datetime object
                        })
                    
                    return reminders
                    
        except Exception as e:
            print(f"❌ Failed to get user reminders: {str(e)}")
            return []
    
    async def mark_reminder_triggered(self, reminder_id: str) -> bool:
        """Mark a reminder as triggered"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        UPDATE user_reminders 
                        SET status = 'triggered', updated_at = NOW()
                        WHERE id = %s
                    """, (reminder_id,))
                    
                    await conn.commit()
                    return True
                    
        except Exception as e:
            print(f"❌ Failed to mark reminder as triggered: {str(e)}")
            return False
    
    async def get_user_profile(self, user_id: str) -> dict:
        """Get user profile from database"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT metadata FROM user_memory 
                        WHERE user_id = %s AND memory_type = 'profile'
                        ORDER BY updated_at DESC LIMIT 1
                    """, (user_id,))
                    
                    row = await cur.fetchone()
                    if row and row[0]:
                        return row[0]  # metadata contains the profile data
                    return {}
        except Exception as e:
            print(f"❌ Failed to get user profile: {str(e)}")
            return {}
    
    async def get_user_count(self) -> int:
        """Get total count of users with profiles"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT COUNT(DISTINCT user_id) FROM user_memory 
                        WHERE memory_type = 'profile'
                    """)
                    result = await cur.fetchone()
                    return result[0] if result else 0
        except Exception as e:
            print(f"❌ Failed to get user count: {str(e)}")
            return 0
    
    async def get_active_reminders_count(self) -> int:
        """Get count of active (pending) reminders"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT COUNT(*) FROM user_reminders 
                        WHERE status = 'pending'
                    """)
                    result = await cur.fetchone()
                    return result[0] if result else 0
        except Exception as e:
            print(f"❌ Failed to get active reminders count: {str(e)}")
            return 0
    
    async def log_progress_change(self, user_id: str, category: str, old_tier: int, 
                                 new_tier: int, change_reason: str = None) -> bool:
        """Log a change in user's needs progress"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        INSERT INTO user_progress_logs 
                        (user_id, category, old_tier, new_tier, change_reason)
                        VALUES (%s, %s, %s, %s, %s)
                    """, (user_id, category, old_tier, new_tier, change_reason))
                    
                    await conn.commit()
                    return True
                    
        except Exception as e:
            print(f"❌ Failed to log progress change: {str(e)}")
            return False
    
    async def delete_user_reminder(self, reminder_id: str) -> bool:
        """Delete a user reminder"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        DELETE FROM user_reminders 
                        WHERE id = %s
                    """, (reminder_id,))
                    
                    await conn.commit()
                    return True
                    
        except Exception as e:
            print(f"❌ Failed to delete user reminder: {str(e)}")
            return False
    
    async def get_user_progress_history(self, user_id: str, category: str = None) -> List[Dict]:
        """Get progress history for a user"""
        try:
            async with await psycopg.AsyncConnection.connect(self.database_url) as conn:
                async with conn.cursor() as cur:
                    if category:
                        await cur.execute("""
                            SELECT category, old_tier, new_tier, change_reason, logged_at
                            FROM user_progress_logs 
                            WHERE user_id = %s AND category = %s
                            ORDER BY logged_at DESC
                        """, (user_id, category))
                    else:
                        await cur.execute("""
                            SELECT category, old_tier, new_tier, change_reason, logged_at
                            FROM user_progress_logs 
                            WHERE user_id = %s
                            ORDER BY logged_at DESC
                        """, (user_id,))
                    
                    progress_logs = []
                    async for row in cur:
                        progress_logs.append({
                            "category": row[0],
                            "old_tier": row[1],
                            "new_tier": row[2],
                            "change_reason": row[3],
                            "logged_at": row[4].isoformat() if row[4] else None
                        })
                    
                    return progress_logs
                    
        except Exception as e:
            print(f"❌ Failed to get progress history: {str(e)}")
            return []
