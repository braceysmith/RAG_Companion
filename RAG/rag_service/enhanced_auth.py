"""
Enhanced Authentication System

This module provides enhanced authentication that maps Unity user IDs to consistent
user identifiers based on email addresses, ensuring the same email creates the same
user account across different devices.
"""

import os
import asyncio
import psycopg
from typing import Optional, Dict, Any
from datetime import datetime
from fastapi import APIRouter, HTTPException, Depends
from pydantic import BaseModel, Field
import logging

from user_mapping import UserMappingService

logger = logging.getLogger(__name__)

# Initialize router
router = APIRouter(prefix="/auth", tags=["enhanced-auth"])

# Initialize user mapping service
user_mapping_service = None

async def get_user_mapping_service():
    """Get or initialize user mapping service"""
    global user_mapping_service
    if user_mapping_service is None:
        database_url = os.getenv("DATABASE_URL", "postgresql://user:password@localhost/rag_db")
        user_mapping_service = UserMappingService(database_url)
        await user_mapping_service.initialize()
    
    return user_mapping_service

# Pydantic models
class UnityAuthRequest(BaseModel):
    unity_user_id: str = Field(..., description="Unity Authentication user ID")
    email: str = Field(..., description="User's email address")
    access_token: Optional[str] = Field(None, description="Unity access token")
    device_id: Optional[str] = Field(None, description="Device identifier")
    platform: Optional[str] = Field(None, description="Platform (iOS, Android, etc.)")
    player_name: Optional[str] = Field(None, description="Player name from Unity")

class UnityAuthResponse(BaseModel):
    success: bool
    primary_user_id: str
    email: str
    is_new_user: bool
    message: str
    user_stats: Optional[Dict[str, Any]] = None

class UserMappingInfo(BaseModel):
    primary_user_id: str
    email: str
    total_devices: int
    platforms: list
    last_seen: str

# Enhanced authentication endpoints

@router.post("/unity/authenticate", response_model=UnityAuthResponse)
async def authenticate_unity_user(request: UnityAuthRequest):
    """
    Authenticate a Unity user and map to consistent user ID based on email.
    This ensures the same email creates the same user account across devices.
    """
    try:
        mapping_service = await get_user_mapping_service()
        
        # Get or create primary user ID based on email
        primary_user_id = await mapping_service.get_or_create_primary_user_id(
            email=request.email,
            unity_user_id=request.unity_user_id,
            device_id=request.device_id,
            platform=request.platform
        )
        
        # Check if this is a new user (first time this email is seen)
        existing_mappings = await mapping_service.get_user_mappings(primary_user_id)
        is_new_user = len(existing_mappings) == 1  # Only one mapping means new user
        
        # Get user stats
        user_stats = await mapping_service.get_user_stats(primary_user_id)
        
        # Create or update user account in the main system
        await ensure_user_account_exists(primary_user_id, request.email, request.player_name)
        
        message = "Authentication successful"
        if is_new_user:
            message = "Welcome! Your account has been created successfully."
        else:
            message = f"Welcome back! You have {user_stats['unique_devices']} device(s) registered."
        
        logger.info(f"Unity user authenticated: {request.email} -> {primary_user_id}")
        
        return UnityAuthResponse(
            success=True,
            primary_user_id=primary_user_id,
            email=request.email,
            is_new_user=is_new_user,
            message=message,
            user_stats=user_stats
        )
        
    except Exception as e:
        logger.error(f"Unity authentication error: {e}")
        raise HTTPException(
            status_code=500,
            detail=f"Authentication failed: {str(e)}"
        )

@router.get("/user/{primary_user_id}/mappings", response_model=list)
async def get_user_mappings(primary_user_id: str):
    """Get all device mappings for a user"""
    try:
        mapping_service = await get_user_mapping_service()
        mappings = await mapping_service.get_user_mappings(primary_user_id)
        return mappings
    except Exception as e:
        logger.error(f"Error getting user mappings: {e}")
        raise HTTPException(
            status_code=500,
            detail=f"Failed to get user mappings: {str(e)}"
        )

@router.get("/user/{primary_user_id}/info", response_model=UserMappingInfo)
async def get_user_info(primary_user_id: str):
    """Get user information and statistics"""
    try:
        mapping_service = await get_user_mapping_service()
        stats = await mapping_service.get_user_stats(primary_user_id)
        
        if not stats['email']:
            raise HTTPException(status_code=404, detail="User not found")
        
        return UserMappingInfo(
            primary_user_id=stats['primary_user_id'],
            email=stats['email'],
            total_devices=stats['unique_devices'],
            platforms=stats['platforms'],
            last_seen=datetime.now().isoformat()
        )
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error getting user info: {e}")
        raise HTTPException(
            status_code=500,
            detail=f"Failed to get user info: {str(e)}"
        )

@router.post("/user/{unity_user_id}/logout")
async def logout_unity_user(unity_user_id: str):
    """Logout a Unity user (deactivate their mapping)"""
    try:
        mapping_service = await get_user_mapping_service()
        await mapping_service.deactivate_user_mapping(unity_user_id)
        
        return {"success": True, "message": "Logout successful"}
    except Exception as e:
        logger.error(f"Logout error: {e}")
        raise HTTPException(
            status_code=500,
            detail=f"Logout failed: {str(e)}"
        )

@router.post("/admin/cleanup-inactive")
async def cleanup_inactive_mappings(days_inactive: int = 30):
    """Clean up inactive user mappings (admin endpoint)"""
    try:
        mapping_service = await get_user_mapping_service()
        affected_rows = await mapping_service.cleanup_inactive_mappings(days_inactive)
        
        return {
            "success": True,
            "message": f"Cleaned up {affected_rows} inactive mappings",
            "affected_rows": affected_rows
        }
    except Exception as e:
        logger.error(f"Cleanup error: {e}")
        raise HTTPException(
            status_code=500,
            detail=f"Cleanup failed: {str(e)}"
        )

async def ensure_user_account_exists(primary_user_id: str, email: str, player_name: Optional[str] = None):
    """Ensure a user account exists in the main user_accounts table"""
    database_url = os.getenv("DATABASE_URL", "postgresql://user:password@localhost/rag_db")
    
    async with await psycopg.AsyncConnection.connect(database_url) as conn:
        async with conn.cursor() as cur:
            # Check if user account exists
            await cur.execute("""
                SELECT user_id FROM user_accounts WHERE user_id = %s
            """, (primary_user_id,))
            
            if await cur.fetchone():
                # User exists, update last_active
                await cur.execute("""
                    UPDATE user_accounts 
                    SET last_active = NOW(), email = %s
                    WHERE user_id = %s
                """, (email, primary_user_id))
            else:
                # Create new user account
                username = player_name or f"User_{primary_user_id[:8]}"
                
                await cur.execute("""
                    INSERT INTO user_accounts (
                        user_id, account_type, username, email, max_prompts, 
                        energy_tokens, prompts_used, created_at, last_active, status
                    ) VALUES (%s, %s, %s, %s, %s, %s, 0, NOW(), NOW(), 'active')
                """, (
                    primary_user_id,
                    'user',  # Default account type
                    username,
                    email,
                    100,  # Default max prompts
                    1000  # Default energy tokens
                ))
            
            await conn.commit()
            logger.info(f"Ensured user account exists: {primary_user_id}")
