"""
Sprig API Endpoints for RAG Service

This module provides REST API endpoints for Sprig management including
creation, retrieval, updating, and content management.
"""

import os
import uuid
import json
from datetime import datetime
from typing import List, Dict, Any, Optional, Union
from fastapi import APIRouter, HTTPException, Depends, Query
from pydantic import BaseModel, Field
from dotenv import load_dotenv

from sprig_management import SprigManager, Sprig, SprigPersonality, SprigAppearance, SprigContent

# Load environment variables
load_dotenv()

# Initialize router
router = APIRouter(prefix="/sprig", tags=["sprig"])

# Initialize Sprig manager
sprig_manager = None

async def get_sprig_manager():
    """Get or initialize Sprig manager"""
    global sprig_manager
    if sprig_manager is None:
        database_url = os.getenv("DATABASE_URL", "postgresql://user:password@localhost/rag_db")
        openai_api_key = os.getenv("OPENAI_API_KEY")
        if not openai_api_key:
            raise HTTPException(status_code=500, detail="OpenAI API key not configured")
        
        sprig_manager = SprigManager(database_url, openai_api_key)
        await sprig_manager.initialize()
    
    return sprig_manager

# Pydantic models for API requests/responses
class SprigPersonalityRequest(BaseModel):
    honesty_humility: float = Field(5.0, ge=1.0, le=10.0)
    emotionality: float = Field(5.0, ge=1.0, le=10.0)
    extraversion: float = Field(5.0, ge=1.0, le=10.0)
    agreeableness: float = Field(5.0, ge=1.0, le=10.0)
    conscientiousness: float = Field(5.0, ge=1.0, le=10.0)
    openness: float = Field(5.0, ge=1.0, le=10.0)

class SprigAppearanceRequest(BaseModel):
    wood_type: str = Field("oak", description="Type of wood for the Sprig")
    color_scheme: str = Field("natural", description="Color scheme")
    size: str = Field("medium", description="Size of the Sprig")
    texture: str = Field("smooth", description="Texture description")
    special_features: List[str] = Field(default_factory=list, description="Special visual features")

class CreateSprigRequest(BaseModel):
    user_id: str = Field(..., description="User ID who owns the Sprig")
    name: str = Field(..., min_length=1, max_length=100, description="Sprig name")
    description: str = Field("", max_length=1000, description="Sprig description")
    backstory: str = Field("", max_length=2000, description="Sprig backstory")
    personality: Optional[SprigPersonalityRequest] = None
    appearance: Optional[SprigAppearanceRequest] = None

class UpdateSprigRequest(BaseModel):
    name: Optional[str] = Field(None, min_length=1, max_length=100)
    description: Optional[str] = Field(None, max_length=1000)
    backstory: Optional[str] = Field(None, max_length=2000)
    personality: Optional[SprigPersonalityRequest] = None
    appearance: Optional[SprigAppearanceRequest] = None
    skills: Optional[List[str]] = None
    preferences: Optional[Dict[str, Any]] = None
    mood: Optional[str] = Field(None, regex="^(neutral|happy|sad|excited|calm|energetic|tired)$")
    energy_level: Optional[int] = Field(None, ge=1, le=10)

class AddContentRequest(BaseModel):
    content_type: str = Field(..., description="Type of content (conversation, memory, preference, skill)")
    content: str = Field(..., min_length=1, description="Content text")
    metadata: Optional[Dict[str, Any]] = None

class SearchContentRequest(BaseModel):
    query: str = Field(..., min_length=1, description="Search query")
    content_type: Optional[str] = None
    top_k: int = Field(5, ge=1, le=20, description="Number of results to return")

class LogConversationRequest(BaseModel):
    user_id: str = Field(..., description="User ID")
    message: str = Field(..., min_length=1, description="User message")
    response: str = Field(..., min_length=1, description="Sprig response")
    session_id: Optional[str] = None
    message_type: str = Field("text", description="Type of message")
    metadata: Optional[Dict[str, Any]] = None

class SprigResponse(BaseModel):
    sprig_id: str
    user_id: str
    name: str
    description: str
    backstory: str
    personality: Dict[str, float]
    appearance: Dict[str, Any]
    skills: List[str]
    preferences: Dict[str, Any]
    mood: str
    energy_level: int
    is_active: bool
    is_default: bool
    created_at: datetime
    updated_at: datetime

class SprigListResponse(BaseModel):
    sprigs: List[SprigResponse]
    total: int

class ContentResponse(BaseModel):
    content_id: str
    sprig_id: str
    content_type: str
    content: str
    metadata: Dict[str, Any]
    created_at: datetime
    updated_at: datetime

class SearchResultResponse(BaseModel):
    content_id: str
    sprig_id: str
    content_type: str
    content: str
    metadata: Dict[str, Any]
    created_at: datetime
    updated_at: datetime
    similarity: float

class ConversationResponse(BaseModel):
    conversation_id: str
    session_id: Optional[str]
    message: str
    response: str
    message_type: str
    metadata: Dict[str, Any]
    created_at: datetime

# API Endpoints

@router.post("/create", response_model=SprigResponse)
async def create_sprig(request: CreateSprigRequest, manager: SprigManager = Depends(get_sprig_manager)):
    """Create a new Sprig for a user"""
    try:
        # Convert request to internal objects
        personality = None
        if request.personality:
            personality = SprigPersonality(
                honesty_humility=request.personality.honesty_humility,
                emotionality=request.personality.emotionality,
                extraversion=request.personality.extraversion,
                agreeableness=request.personality.agreeableness,
                conscientiousness=request.personality.conscientiousness,
                openness=request.personality.openness
            )
        
        appearance = None
        if request.appearance:
            appearance = SprigAppearance(
                wood_type=request.appearance.wood_type,
                color_scheme=request.appearance.color_scheme,
                size=request.appearance.size,
                texture=request.appearance.texture,
                special_features=request.appearance.special_features
            )
        
        # Create the Sprig
        sprig = await manager.create_sprig(
            user_id=request.user_id,
            name=request.name,
            description=request.description,
            backstory=request.backstory,
            personality=personality,
            appearance=appearance
        )
        
        # Convert to response format
        return SprigResponse(
            sprig_id=sprig.sprig_id,
            user_id=sprig.user_id,
            name=sprig.name,
            description=sprig.description,
            backstory=sprig.backstory,
            personality={
                "honesty_humility": sprig.personality.honesty_humility,
                "emotionality": sprig.personality.emotionality,
                "extraversion": sprig.personality.extraversion,
                "agreeableness": sprig.personality.agreeableness,
                "conscientiousness": sprig.personality.conscientiousness,
                "openness": sprig.personality.openness
            },
            appearance={
                "wood_type": sprig.appearance.wood_type,
                "color_scheme": sprig.appearance.color_scheme,
                "size": sprig.appearance.size,
                "texture": sprig.appearance.texture,
                "special_features": sprig.appearance.special_features
            },
            skills=sprig.skills,
            preferences=sprig.preferences,
            mood=sprig.mood,
            energy_level=sprig.energy_level,
            is_active=sprig.is_active,
            is_default=sprig.is_default,
            created_at=sprig.created_at,
            updated_at=sprig.updated_at
        )
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to create Sprig: {str(e)}")

@router.get("/{sprig_id}", response_model=SprigResponse)
async def get_sprig(sprig_id: str, manager: SprigManager = Depends(get_sprig_manager)):
    """Get a specific Sprig by ID"""
    try:
        sprig = await manager.get_sprig(sprig_id)
        if not sprig:
            raise HTTPException(status_code=404, detail="Sprig not found")
        
        return SprigResponse(
            sprig_id=sprig.sprig_id,
            user_id=sprig.user_id,
            name=sprig.name,
            description=sprig.description,
            backstory=sprig.backstory,
            personality={
                "honesty_humility": sprig.personality.honesty_humility,
                "emotionality": sprig.personality.emotionality,
                "extraversion": sprig.personality.extraversion,
                "agreeableness": sprig.personality.agreeableness,
                "conscientiousness": sprig.personality.conscientiousness,
                "openness": sprig.personality.openness
            },
            appearance={
                "wood_type": sprig.appearance.wood_type,
                "color_scheme": sprig.appearance.color_scheme,
                "size": sprig.appearance.size,
                "texture": sprig.appearance.texture,
                "special_features": sprig.appearance.special_features
            },
            skills=sprig.skills,
            preferences=sprig.preferences,
            mood=sprig.mood,
            energy_level=sprig.energy_level,
            is_active=sprig.is_active,
            is_default=sprig.is_default,
            created_at=sprig.created_at,
            updated_at=sprig.updated_at
        )
        
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get Sprig: {str(e)}")

@router.get("/user/{user_id}", response_model=SprigListResponse)
async def get_user_sprigs(user_id: str, active_only: bool = True, 
                          manager: SprigManager = Depends(get_sprig_manager)):
    """Get all Sprigs for a user"""
    try:
        sprigs = await manager.get_user_sprigs(user_id, active_only)
        
        sprig_responses = []
        for sprig in sprigs:
            sprig_responses.append(SprigResponse(
                sprig_id=sprig.sprig_id,
                user_id=sprig.user_id,
                name=sprig.name,
                description=sprig.description,
                backstory=sprig.backstory,
                personality={
                    "honesty_humility": sprig.personality.honesty_humility,
                    "emotionality": sprig.personality.emotionality,
                    "extraversion": sprig.personality.extraversion,
                    "agreeableness": sprig.personality.agreeableness,
                    "conscientiousness": sprig.personality.conscientiousness,
                    "openness": sprig.personality.openness
                },
                appearance={
                    "wood_type": sprig.appearance.wood_type,
                    "color_scheme": sprig.appearance.color_scheme,
                    "size": sprig.appearance.size,
                    "texture": sprig.appearance.texture,
                    "special_features": sprig.appearance.special_features
                },
                skills=sprig.skills,
                preferences=sprig.preferences,
                mood=sprig.mood,
                energy_level=sprig.energy_level,
                is_active=sprig.is_active,
                is_default=sprig.is_default,
                created_at=sprig.created_at,
                updated_at=sprig.updated_at
            ))
        
        return SprigListResponse(sprigs=sprig_responses, total=len(sprig_responses))
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get user Sprigs: {str(e)}")

@router.get("/user/{user_id}/default", response_model=SprigResponse)
async def get_default_sprig(user_id: str, manager: SprigManager = Depends(get_sprig_manager)):
    """Get the default Sprig for a user"""
    try:
        sprig = await manager.get_default_sprig(user_id)
        if not sprig:
            raise HTTPException(status_code=404, detail="No default Sprig found")
        
        return SprigResponse(
            sprig_id=sprig.sprig_id,
            user_id=sprig.user_id,
            name=sprig.name,
            description=sprig.description,
            backstory=sprig.backstory,
            personality={
                "honesty_humility": sprig.personality.honesty_humility,
                "emotionality": sprig.personality.emotionality,
                "extraversion": sprig.personality.extraversion,
                "agreeableness": sprig.personality.agreeableness,
                "conscientiousness": sprig.personality.conscientiousness,
                "openness": sprig.personality.openness
            },
            appearance={
                "wood_type": sprig.appearance.wood_type,
                "color_scheme": sprig.appearance.color_scheme,
                "size": sprig.appearance.size,
                "texture": sprig.appearance.texture,
                "special_features": sprig.appearance.special_features
            },
            skills=sprig.skills,
            preferences=sprig.preferences,
            mood=sprig.mood,
            energy_level=sprig.energy_level,
            is_active=sprig.is_active,
            is_default=sprig.is_default,
            created_at=sprig.created_at,
            updated_at=sprig.updated_at
        )
        
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get default Sprig: {str(e)}")

@router.put("/{sprig_id}/default", response_model=Dict[str, bool])
async def set_default_sprig(sprig_id: str, user_id: str, 
                           manager: SprigManager = Depends(get_sprig_manager)):
    """Set a Sprig as the default for a user"""
    try:
        success = await manager.set_default_sprig(user_id, sprig_id)
        if not success:
            raise HTTPException(status_code=404, detail="Sprig not found or not owned by user")
        
        return {"success": True}
        
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to set default Sprig: {str(e)}")

@router.put("/{sprig_id}", response_model=Dict[str, bool])
async def update_sprig(sprig_id: str, request: UpdateSprigRequest,
                      manager: SprigManager = Depends(get_sprig_manager)):
    """Update a Sprig"""
    try:
        # Convert request to update dictionary
        updates = {}
        
        if request.name is not None:
            updates["name"] = request.name
        if request.description is not None:
            updates["description"] = request.description
        if request.backstory is not None:
            updates["backstory"] = request.backstory
        if request.personality is not None:
            updates["personality"] = {
                "honesty_humility": request.personality.honesty_humility,
                "emotionality": request.personality.emotionality,
                "extraversion": request.personality.extraversion,
                "agreeableness": request.personality.agreeableness,
                "conscientiousness": request.personality.conscientiousness,
                "openness": request.personality.openness
            }
        if request.appearance is not None:
            updates["appearance"] = {
                "wood_type": request.appearance.wood_type,
                "color_scheme": request.appearance.color_scheme,
                "size": request.appearance.size,
                "texture": request.appearance.texture,
                "special_features": request.appearance.special_features
            }
        if request.skills is not None:
            updates["skills"] = request.skills
        if request.preferences is not None:
            updates["preferences"] = request.preferences
        if request.mood is not None:
            updates["mood"] = request.mood
        if request.energy_level is not None:
            updates["energy_level"] = request.energy_level
        
        success = await manager.update_sprig(sprig_id, updates)
        if not success:
            raise HTTPException(status_code=404, detail="Sprig not found")
        
        return {"success": True}
        
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to update Sprig: {str(e)}")

@router.delete("/{sprig_id}", response_model=Dict[str, bool])
async def delete_sprig(sprig_id: str, user_id: str,
                      manager: SprigManager = Depends(get_sprig_manager)):
    """Delete a Sprig (soft delete)"""
    try:
        success = await manager.delete_sprig(sprig_id, user_id)
        if not success:
            raise HTTPException(status_code=404, detail="Sprig not found or not owned by user")
        
        return {"success": True}
        
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to delete Sprig: {str(e)}")

# Content Management Endpoints

@router.post("/{sprig_id}/content", response_model=Dict[str, str])
async def add_content(sprig_id: str, request: AddContentRequest,
                     manager: SprigManager = Depends(get_sprig_manager)):
    """Add content to a Sprig"""
    try:
        content_id = await manager.add_sprig_content(
            sprig_id=sprig_id,
            content_type=request.content_type,
            content=request.content,
            metadata=request.metadata
        )
        
        return {"content_id": content_id, "success": True}
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to add content: {str(e)}")

@router.get("/{sprig_id}/content", response_model=List[ContentResponse])
async def get_content(sprig_id: str, content_type: Optional[str] = None,
                     limit: int = Query(50, ge=1, le=100),
                     manager: SprigManager = Depends(get_sprig_manager)):
    """Get content for a Sprig"""
    try:
        content_list = await manager.get_sprig_content(sprig_id, content_type, limit)
        
        return [
            ContentResponse(
                content_id=content.content_id,
                sprig_id=content.sprig_id,
                content_type=content.content_type,
                content=content.content,
                metadata=content.metadata,
                created_at=content.created_at,
                updated_at=content.updated_at
            )
            for content in content_list
        ]
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get content: {str(e)}")

@router.post("/{sprig_id}/search", response_model=List[SearchResultResponse])
async def search_content(sprig_id: str, request: SearchContentRequest,
                        manager: SprigManager = Depends(get_sprig_manager)):
    """Search Sprig content using semantic similarity"""
    try:
        results = await manager.search_sprig_content(
            sprig_id=sprig_id,
            query=request.query,
            content_type=request.content_type,
            top_k=request.top_k
        )
        
        return [
            SearchResultResponse(
                content_id=result["content_id"],
                sprig_id=result["sprig_id"],
                content_type=result["content_type"],
                content=result["content"],
                metadata=result["metadata"],
                created_at=result["created_at"],
                updated_at=result["updated_at"],
                similarity=result["similarity"]
            )
            for result in results
        ]
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to search content: {str(e)}")

# Conversation Management Endpoints

@router.post("/{sprig_id}/conversation", response_model=Dict[str, str])
async def log_conversation(sprig_id: str, request: LogConversationRequest,
                          manager: SprigManager = Depends(get_sprig_manager)):
    """Log a conversation with a Sprig"""
    try:
        conversation_id = await manager.log_conversation(
            sprig_id=sprig_id,
            user_id=request.user_id,
            message=request.message,
            response=request.response,
            session_id=request.session_id,
            message_type=request.message_type,
            metadata=request.metadata
        )
        
        return {"conversation_id": conversation_id, "success": True}
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to log conversation: {str(e)}")

@router.get("/{sprig_id}/conversations", response_model=List[ConversationResponse])
async def get_conversation_history(sprig_id: str, user_id: str,
                                  limit: int = Query(50, ge=1, le=100),
                                  manager: SprigManager = Depends(get_sprig_manager)):
    """Get conversation history for a Sprig"""
    try:
        conversations = await manager.get_conversation_history(sprig_id, user_id, limit)
        
        return [
            ConversationResponse(
                conversation_id=conv["conversation_id"],
                session_id=conv["session_id"],
                message=conv["message"],
                response=conv["response"],
                message_type=conv["message_type"],
                metadata=conv["metadata"],
                created_at=conv["created_at"]
            )
            for conv in conversations
        ]
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get conversation history: {str(e)}")
