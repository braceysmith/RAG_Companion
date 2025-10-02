"""
Companion API for RAG Companion System

This module provides FastAPI endpoints for the companion system,
including user management, companion interactions, and content storage.
"""

from fastapi import FastAPI, HTTPException, Depends, status, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from pydantic import BaseModel, Field
from typing import Dict, List, Optional, Any, Union
import logging
import json
import asyncio
from datetime import datetime
from dataclasses import asdict

from .companion_system.companion_manager import CompanionManager
from .companion_system.companion_core import CompanionCore
from .companion_system.wellbeing_framework import WellbeingDimension
from .companion_system.communication_system import CommunicationSystem

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Initialize FastAPI app
app = FastAPI(
    title="RAG Companion API",
    description="API for RAG Companion System with personality and wellbeing support",
    version="2.0.0"
)

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure appropriately for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Security
security = HTTPBearer()

# Initialize companion manager and communication system
companion_manager = CompanionManager()
communication_system = CommunicationSystem()

# WebSocket connection manager
class ConnectionManager:
    def __init__(self):
        self.active_connections: Dict[str, WebSocket] = {}
        self.user_companion_map: Dict[str, str] = {}  # user_id -> companion_id

    async def connect(self, websocket: WebSocket, user_id: str, companion_id: str):
        await websocket.accept()
        self.active_connections[user_id] = websocket
        self.user_companion_map[user_id] = companion_id
        logger.info(f"WebSocket connected for user {user_id} with companion {companion_id}")

    def disconnect(self, user_id: str):
        if user_id in self.active_connections:
            del self.active_connections[user_id]
        if user_id in self.user_companion_map:
            del self.user_companion_map[user_id]
        logger.info(f"WebSocket disconnected for user {user_id}")

    async def send_personal_message(self, message: str, user_id: str):
        if user_id in self.active_connections:
            await self.active_connections[user_id].send_text(json.dumps({
                "type": "companion_message",
                "message": message,
                "timestamp": datetime.now().isoformat()
            }))

    async def broadcast(self, message: str, exclude_user: str = None):
        for user_id, connection in self.active_connections.items():
            if user_id != exclude_user:
                try:
                    await connection.send_text(json.dumps({
                        "type": "broadcast",
                        "message": message,
                        "timestamp": datetime.now().isoformat()
                    }))
                except Exception as e:
                    logger.error(f"Failed to send broadcast to {user_id}: {e}")

manager = ConnectionManager()

# Pydantic models for request/response
class UserCreateRequest(BaseModel):
    username: str = Field(..., min_length=3, max_length=50)
    email: str = Field(..., pattern=r"^[^@]+@[^@]+\.[^@]+$")
    initial_preferences: Optional[Dict[str, Any]] = Field(default_factory=dict)

class UserLoginRequest(BaseModel):
    username: str
    password_hash: Optional[str] = None  # Simplified for demo

class CompanionCreateRequest(BaseModel):
    name: str = Field(..., min_length=2, max_length=50)
    personality_scores: Optional[Dict[str, float]] = None
    personality_preset: Optional[str] = None

class MessageRequest(BaseModel):
    message: str = Field(..., min_length=1, max_length=2000)
    context: Optional[Dict[str, Any]] = None

class WellbeingAssessmentRequest(BaseModel):
    dimension: str = Field(..., description="Wellbeing dimension (essential, safety, belonging, etc.)")
    metric: str = Field(..., description="Specific metric within the dimension")
    assessment: str = Field(..., description="User's assessment of their wellbeing")
    notes: Optional[str] = Field(default="", max_length=1000)

class ContentStoreRequest(BaseModel):
    content_type: str = Field(..., description="Type of content (text, image, audio, etc.)")
    title: str = Field(..., min_length=1, max_length=200)
    content: str = Field(..., min_length=1)
    metadata: Optional[Dict[str, Any]] = None
    tags: Optional[List[str]] = Field(default_factory=list)

class ContentSearchRequest(BaseModel):
    query: str = Field(..., min_length=1)
    content_type: Optional[str] = None
    tags: Optional[List[str]] = None
    limit: Optional[int] = Field(default=50, ge=1, le=100)

# Dependency to get current user from token
async def get_current_user(credentials: HTTPAuthorizationCredentials = Depends(security)) -> str:
    """Extract user ID from authorization token"""
    # Simplified token validation for demo
    # In production, this would validate JWT tokens and extract user info
    try:
        # For demo purposes, assume token is the user_id
        user_id = credentials.credentials
        if user_id not in companion_manager.users:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Invalid authentication token"
            )
        return user_id
    except Exception as e:
        logger.error(f"Authentication error: {e}")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid authentication token"
        )

# Health check endpoint
@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {
        "status": "healthy",
        "timestamp": datetime.now().isoformat(),
        "version": "2.0.0",
        "system": "RAG Companion API"
    }

# System statistics
@app.get("/system/stats")
async def get_system_stats():
    """Get system statistics"""
    try:
        stats = companion_manager.get_system_stats()
        return {
            "success": True,
            "stats": stats,
            "timestamp": datetime.now().isoformat()
        }
    except Exception as e:
        logger.error(f"Error getting system stats: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to retrieve system statistics"
        )

# User management endpoints
@app.post("/users/register", status_code=status.HTTP_201_CREATED)
async def register_user(request: UserCreateRequest):
    """Register a new user account"""
    try:
        result = companion_manager.create_user_account(
            username=request.username,
            email=request.email,
            initial_preferences=request.initial_preferences
        )
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error registering user: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to create user account"
        )

@app.post("/users/login")
async def login_user(request: UserLoginRequest):
    """Authenticate a user"""
    try:
        result = companion_manager.authenticate_user(
            username=request.username,
            password_hash=request.password_hash
        )
        
        if result["success"]:
            # In production, return a proper JWT token
            return {
                **result,
                "access_token": result["user_id"],  # Simplified for demo
                "token_type": "bearer"
            }
        else:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error authenticating user: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Authentication failed"
        )

# Companion management endpoints
@app.get("/companions")
async def get_user_companions(user_id: str = Depends(get_current_user)):
    """Get all companions for the current user"""
    try:
        result = companion_manager.get_user_companions(user_id)
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error getting user companions: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to retrieve companions"
        )

@app.post("/companions/create", status_code=status.HTTP_201_CREATED)
async def create_custom_companion(
    request: CompanionCreateRequest,
    user_id: str = Depends(get_current_user)
):
    """Create a custom companion for the user"""
    try:
        result = companion_manager.create_custom_companion(
            user_id=user_id,
            name=request.name,
            personality_scores=request.personality_scores,
            personality_preset=request.personality_preset
        )
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error creating custom companion: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to create custom companion"
        )

@app.post("/companions/{companion_id}/switch")
async def switch_active_companion(
    companion_id: str,
    user_id: str = Depends(get_current_user)
):
    """Switch the user's active companion"""
    try:
        result = companion_manager.switch_active_companion(user_id, companion_id)
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error switching companion: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to switch companion"
        )

# Companion interaction endpoints
@app.get("/companions/active/greeting")
async def get_active_companion_greeting(user_id: str = Depends(get_current_user)):
    """Get a greeting from the user's active companion"""
    try:
        companion = companion_manager.get_active_companion(user_id)
        
        if not companion:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="No active companion found"
            )
        
        greeting = companion.get_greeting()
        
        return {
            "success": True,
            "companion_id": companion.companion_id,
            "companion_name": companion.name,
            "greeting": greeting,
            "timestamp": datetime.now().isoformat()
        }
        
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error getting companion greeting: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to get companion greeting"
        )

@app.post("/companions/active/message")
async def send_message_to_companion(
    request: MessageRequest,
    user_id: str = Depends(get_current_user)
):
    """Send a message to the user's active companion"""
    try:
        companion = companion_manager.get_active_companion(user_id)
        
        if not companion:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="No active companion found"
            )
        
        response = companion.process_message(
            user_message=request.message,
            context=request.context
        )
        
        return {
            "success": True,
            "companion_id": companion.companion_id,
            "companion_name": companion.name,
            "response": response,
            "timestamp": datetime.now().isoformat()
        }
        
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error processing message: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to process message"
        )

@app.get("/companions/active/capabilities")
async def get_active_companion_capabilities(user_id: str = Depends(get_current_user)):
    """Get capabilities of the user's active companion"""
    try:
        companion = companion_manager.get_active_companion(user_id)
        
        if not companion:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="No active companion found"
            )
        
        capabilities = companion.get_capabilities()
        
        return {
            "success": True,
            "companion_id": companion.companion_id,
            "companion_name": companion.name,
            "capabilities": capabilities,
            "timestamp": datetime.now().isoformat()
        }
        
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error getting companion capabilities: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to get companion capabilities"
        )

# Wellbeing endpoints
@app.post("/wellbeing/assess")
async def assess_wellbeing(
    request: WellbeingAssessmentRequest,
    user_id: str = Depends(get_current_user)
):
    """Assess user wellbeing in a specific area"""
    try:
        companion = companion_manager.get_active_companion(user_id)
        
        if not companion:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="No active companion found"
            )
        
        result = companion.assess_wellbeing(
            dimension=request.dimension,
            metric=request.metric,
            assessment=request.assessment,
            notes=request.notes
        )
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error assessing wellbeing: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to assess wellbeing"
        )

@app.get("/wellbeing/summary")
async def get_wellbeing_summary(user_id: str = Depends(get_current_user)):
    """Get comprehensive wellbeing summary"""
    try:
        companion = companion_manager.get_active_companion(user_id)
        
        if not companion:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="No active companion found"
            )
        
        result = companion.get_wellbeing_summary()
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error getting wellbeing summary: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to get wellbeing summary"
        )

@app.get("/wellbeing/dimensions")
async def get_wellbeing_dimensions():
    """Get available wellbeing dimensions and metrics"""
    try:
        # Get all available dimensions
        dimensions = {}
        for dimension in WellbeingDimension:
            dimensions[dimension.value] = {
                "name": dimension.value.replace("_", " ").title(),
                "description": f"Focuses on {dimension.value.replace('_', ' ')} aspects of wellbeing"
            }
        
        return {
            "success": True,
            "dimensions": dimensions,
            "total_dimensions": len(dimensions),
            "timestamp": datetime.now().isoformat()
        }
        
    except Exception as e:
        logger.error(f"Error getting wellbeing dimensions: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to get wellbeing dimensions"
        )

# Content management endpoints
@app.post("/content/store", status_code=status.HTTP_201_CREATED)
async def store_generated_content(
    request: ContentStoreRequest,
    user_id: str = Depends(get_current_user)
):
    """Store content generated by a companion"""
    try:
        companion = companion_manager.get_active_companion(user_id)
        
        if not companion:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="No active companion found"
            )
        
        result = companion_manager.store_generated_content(
            companion_id=companion.companion_id,
            user_id=user_id,
            content_type=request.content_type,
            title=request.title,
            content=request.content,
            metadata=request.metadata,
            tags=request.tags
        )
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error storing content: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to store content"
        )

@app.get("/content")
async def get_user_content(
    content_type: Optional[str] = None,
    companion_id: Optional[str] = None,
    limit: Optional[int] = 50,
    user_id: str = Depends(get_current_user)
):
    """Get content generated for the current user"""
    try:
        result = companion_manager.get_user_generated_content(
            user_id=user_id,
            content_type=content_type,
            companion_id=companion_id,
            limit=limit
        )
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error getting user content: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to retrieve content"
        )

@app.post("/content/search")
async def search_user_content(
    request: ContentSearchRequest,
    user_id: str = Depends(get_current_user)
):
    """Search through user's generated content"""
    try:
        result = companion_manager.search_generated_content(
            user_id=user_id,
            query=request.query,
            content_type=request.content_type,
            tags=request.tags
        )
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error searching content: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to search content"
        )

@app.delete("/content/{content_id}")
async def delete_content(
    content_id: str,
    user_id: str = Depends(get_current_user)
):
    """Delete generated content"""
    try:
        result = companion_manager.delete_generated_content(user_id, content_id)
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error deleting content: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to delete content"
        )

# User preferences and goals
@app.post("/users/preferences/{key}")
async def set_user_preference(
    key: str,
    value: Any,
    user_id: str = Depends(get_current_user)
):
    """Set a user preference"""
    try:
        companion = companion_manager.get_active_companion(user_id)
        
        if not companion:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="No active companion found"
            )
        
        result = companion.set_user_preference(key, value)
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error setting user preference: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to set user preference"
        )

@app.get("/users/preferences")
async def get_user_preferences(user_id: str = Depends(get_current_user)):
    """Get user preferences"""
    try:
        companion = companion_manager.get_active_companion(user_id)
        
        if not companion:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="No active companion found"
            )
        
        result = companion.get_user_preferences()
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error getting user preferences: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to get user preferences"
        )

@app.post("/users/goals")
async def add_user_goal(
    goal: str,
    user_id: str = Depends(get_current_user)
):
    """Add a user goal"""
    try:
        companion = companion_manager.get_active_companion(user_id)
        
        if not companion:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="No active companion found"
            )
        
        result = companion.add_user_goal(goal)
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error adding user goal: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to add user goal"
        )

@app.get("/users/goals")
async def get_user_goals(user_id: str = Depends(get_current_user)):
    """Get user goals"""
    try:
        companion = companion_manager.get_active_companion(user_id)
        
        if not companion:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="No active companion found"
            )
        
        result = companion.get_user_goals()
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error getting user goals: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to get user goals"
        )

# Performance and analytics
@app.get("/companions/{companion_id}/performance")
async def get_companion_performance(
    companion_id: str,
    user_id: str = Depends(get_current_user)
):
    """Get performance metrics for a companion"""
    try:
        # Verify user has access to this companion
        user_companions = companion_manager.get_user_companions(user_id)
        if not user_companions["success"]:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=user_companions["message"]
            )
        
        if companion_id not in user_companions["companions"]:
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Access denied to this companion"
            )
        
        result = companion_manager.get_companion_performance(companion_id)
        
        if result["success"]:
            return result
        else:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=result["message"]
            )
            
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error getting companion performance: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to get companion performance"
        )

# WebSocket endpoints for real-time chat
@app.websocket("/ws/chat/{user_id}/{companion_id}")
async def websocket_chat_endpoint(websocket: WebSocket, user_id: str, companion_id: str):
    """WebSocket endpoint for real-time chat with a companion"""
    try:
        # Verify user has access to this companion
        user_companions = companion_manager.get_user_companions(user_id)
        if not any(c.companion_id == companion_id for c in user_companions):
            await websocket.close(code=4003, reason="Access denied to companion")
            return

        # Connect to WebSocket
        await manager.connect(websocket, user_id, companion_id)
        
        # Get the companion instance
        companion = companion_manager.get_companion(companion_id)
        if not companion:
            await websocket.close(code=4004, reason="Companion not found")
            return

        # Send initial greeting
        greeting = companion.get_greeting()
        await manager.send_personal_message(greeting, user_id)

        try:
            while True:
                # Receive message from client
                data = await websocket.receive_text()
                message_data = json.loads(data)
                
                message_type = message_data.get("type", "text")
                content = message_data.get("content", "")
                
                if message_type == "text":
                    # Process text message through companion
                    response = companion.process_user_message(user_id, content, "text")
                    await manager.send_personal_message(response, user_id)
                    
                elif message_type == "voice":
                    # Process voice message (convert to text first)
                    # This would integrate with the communication system's STT
                    response = companion.process_user_message(user_id, content, "voice")
                    await manager.send_personal_message(response, user_id)
                    
                elif message_type == "image":
                    # Process image message
                    # This would integrate with the communication system's image processing
                    response = companion.process_user_message(user_id, content, "image")
                    await manager.send_personal_message(response, user_id)
                    
                elif message_type == "typing":
                    # Handle typing indicator
                    await manager.broadcast(json.dumps({
                        "type": "typing_indicator",
                        "user_id": user_id,
                        "is_typing": message_data.get("is_typing", False)
                    }), exclude_user=user_id)
                    
                elif message_type == "ping":
                    # Respond to ping
                    await websocket.send_text(json.dumps({"type": "pong"}))
                    
        except WebSocketDisconnect:
            logger.info(f"WebSocket disconnected for user {user_id}")
        except Exception as e:
            logger.error(f"WebSocket error for user {user_id}: {e}")
            await websocket.send_text(json.dumps({
                "type": "error",
                "message": "An error occurred while processing your message"
            }))
            
    except Exception as e:
        logger.error(f"Failed to establish WebSocket connection: {e}")
        try:
            await websocket.close(code=4000, reason="Internal server error")
        except:
            pass
    finally:
        manager.disconnect(user_id)

@app.websocket("/ws/companion/{companion_id}/status")
async def websocket_companion_status_endpoint(websocket: WebSocket, companion_id: str):
    """WebSocket endpoint for monitoring companion status and actions"""
    try:
        await websocket.accept()
        
        # Get companion instance
        companion = companion_manager.get_companion(companion_id)
        if not companion:
            await websocket.close(code=4004, reason="Companion not found")
            return

        # Send initial companion status
        capabilities = companion.get_companion_capabilities()
        await websocket.send_text(json.dumps({
            "type": "companion_status",
            "companion_id": companion_id,
            "name": companion.name,
            "capabilities": capabilities,
            "timestamp": datetime.now().isoformat()
        }))

        try:
            while True:
                # Keep connection alive and monitor for status updates
                await asyncio.sleep(30)  # Send heartbeat every 30 seconds
                await websocket.send_text(json.dumps({
                    "type": "heartbeat",
                    "timestamp": datetime.now().isoformat()
                }))
                
        except WebSocketDisconnect:
            logger.info(f"Companion status WebSocket disconnected for {companion_id}")
        except Exception as e:
            logger.error(f"Companion status WebSocket error for {companion_id}: {e}")
            
    except Exception as e:
        logger.error(f"Failed to establish companion status WebSocket: {e}")
        try:
            await websocket.close(code=4000, reason="Internal server error")
        except:
            pass

# Skills and memory endpoints
@app.post("/companions/{companion_id}/skills/execute")
async def execute_companion_skill(
    companion_id: str,
    skill_name: str,
    parameters: Dict[str, Any],
    user_id: str = Depends(get_current_user)
):
    """Execute a specific skill through the companion"""
    try:
        companion = companion_manager.get_companion(companion_id)
        if not companion:
            raise HTTPException(status_code=404, detail="Companion not found")
            
        # Verify user has access to this companion
        user_companions = companion_manager.get_user_companions(user_id)
        if not any(c.companion_id == companion_id for c in user_companions):
            raise HTTPException(status_code=403, detail="Access denied to companion")
        
        # Execute skill through companion's skills manager
        result = companion.skills_manager.execute_skill(skill_name, user_id, parameters)
        
        return {
            "success": result.success,
            "data": result.data,
            "message": result.message,
            "execution_time": result.execution_time
        }
        
    except Exception as e:
        logger.error(f"Error executing skill {skill_name}: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to execute skill: {str(e)}")

@app.get("/companions/{companion_id}/skills")
async def get_companion_skills(
    companion_id: str,
    category: Optional[str] = None,
    user_id: str = Depends(get_current_user)
):
    """Get available skills for a companion"""
    try:
        companion = companion_manager.get_companion(companion_id)
        if not companion:
            raise HTTPException(status_code=404, detail="Companion not found")
            
        # Verify user has access to this companion
        user_companions = companion_manager.get_user_companions(user_id)
        if not any(c.companion_id == companion_id for c in user_companions):
            raise HTTPException(status_code=403, detail="Access denied to companion")
        
        if category:
            skills = companion.skills_manager.list_skills(category)
        else:
            skills = companion.skills_manager.list_skills()
            
        return {
            "companion_id": companion_id,
            "skills": [
                {
                    "name": skill.name,
                    "description": skill.description,
                    "category": skill.category.value,
                    "parameters": [asdict(param) for param in skill.parameters],
                    "usage_examples": skill.get_usage_examples()
                }
                for skill in skills
            ]
        }
        
    except Exception as e:
        logger.error(f"Error getting companion skills: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to get skills: {str(e)}")

@app.post("/companions/{companion_id}/skills/suggest")
async def suggest_companion_skills(
    companion_id: str,
    message: str,
    user_id: str = Depends(get_current_user)
):
    """Get skill suggestions based on user message"""
    try:
        companion = companion_manager.get_companion(companion_id)
        if not companion:
            raise HTTPException(status_code=404, detail="Companion not found")
            
        # Verify user has access to this companion
        user_companions = companion_manager.get_user_companions(user_id)
        if not any(c.companion_id == companion_id for c in user_companions):
            raise HTTPException(status_code=403, detail="Access denied to companion")
        
        # Get user context for skill suggestions
        user_context = companion.get_user_context(user_id) if hasattr(companion, 'get_user_context') else {}
        
        # Get skill suggestions
        suggestions = companion.skills_manager.suggest_skills(message, user_context)
        
        return {
            "companion_id": companion_id,
            "message": message,
            "suggestions": [
                {
                    "skill_name": skill.name,
                    "relevance_score": score,
                    "description": skill.description,
                    "category": skill.category.value
                }
                for skill, score in suggestions
            ]
        }
        
    except Exception as e:
        logger.error(f"Error suggesting skills: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to suggest skills: {str(e)}")

@app.get("/companions/{companion_id}/memory/search")
async def search_companion_memory(
    companion_id: str,
    query: str,
    memory_types: Optional[List[str]] = None,
    top_k: int = 5,
    user_id: str = Depends(get_current_user)
):
    """Search companion's memory for relevant information"""
    try:
        companion = companion_manager.get_companion(companion_id)
        if not companion:
            raise HTTPException(status_code=404, detail="Companion not found")
            
        # Verify user has access to this companion
        user_companions = companion_manager.get_user_companions(user_id)
        if not any(c.companion_id == companion_id for c in user_companions):
            raise HTTPException(status_code=403, detail="Access denied to companion")
        
        # Search memory
        results = companion.memory_system.search_memories(
            user_id=user_id,
            query=query,
            memory_types=memory_types or ["episodic", "semantic", "profile"],
            top_k=top_k
        )
        
        return {
            "companion_id": companion_id,
            "query": query,
            "results": [
                {
                    "memory_id": result.memory_id,
                    "memory_type": result.memory_type,
                    "content": result.content,
                    "metadata": result.metadata,
                    "score": result.score
                }
                for result in results
            ]
        }
        
    except Exception as e:
        logger.error(f"Error searching companion memory: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to search memory: {str(e)}")

@app.post("/companions/{companion_id}/memory/store")
async def store_companion_memory(
    companion_id: str,
    memory_type: str,
    content: str,
    metadata: Optional[Dict[str, Any]] = None,
    user_id: str = Depends(get_current_user)
):
    """Store new memory in companion's memory system"""
    try:
        companion = companion_manager.get_companion(companion_id)
        if not companion:
            raise HTTPException(status_code=404, detail="Companion not found")
            
        # Verify user has access to this companion
        user_companions = companion_manager.get_user_companions(user_id)
        if not any(c.companion_id == companion_id for c in user_companions):
            raise HTTPException(status_code=403, detail="Access denied to companion")
        
        # Store memory
        memory_id = companion.memory_system.store_memory(
            user_id=user_id,
            memory_type=memory_type,
            content=content,
            metadata=metadata or {}
        )
        
        return {
            "companion_id": companion_id,
            "memory_id": memory_id,
            "status": "stored"
        }
        
    except Exception as e:
        logger.error(f"Error storing companion memory: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to store memory: {str(e)}")

@app.get("/companions/{companion_id}/context")
async def get_companion_context(
    companion_id: str,
    user_id: str = Depends(get_current_user)
):
    """Get comprehensive context for a user from the companion"""
    try:
        companion = companion_manager.get_companion(companion_id)
        if not companion:
            raise HTTPException(status_code=404, detail="Companion not found")
            
        # Verify user has access to this companion
        user_companions = companion_manager.get_user_companions(user_id)
        if not any(c.companion_id == companion_id for c in user_companions):
            raise HTTPException(status_code=403, detail="Access denied to companion")
        
        # Get user context
        user_context = companion.user_contexts.get(user_id, {})
        
        # Get upcoming reminders and events
        upcoming_reminders = companion.get_upcoming_reminders(user_id, days_ahead=7)
        upcoming_events = companion.get_upcoming_events(user_id, days_ahead=7)
        goal_summary = companion.get_goal_summary(user_id)
        daily_summary = companion.get_daily_summary(user_id)
        
        return {
            "companion_id": companion_id,
            "user_context": user_context,
            "upcoming_reminders": [asdict(reminder) for reminder in upcoming_reminders],
            "upcoming_events": [asdict(event) for event in upcoming_events],
            "goal_summary": goal_summary,
            "daily_summary": daily_summary
        }
        
    except Exception as e:
        logger.error(f"Error getting companion context: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to get context: {str(e)}")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
