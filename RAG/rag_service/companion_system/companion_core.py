"""
Core Companion Class for RAG Companions

This module implements the core companion functionality that integrates
personality, wellbeing support, and communication capabilities.
"""

import json
import logging
import uuid
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple, Union
from dataclasses import dataclass, asdict

from .personality_engine import PersonalityEngine, PersonalityScores
from .wellbeing_framework import WellbeingFramework, WellbeingAssessment
from .memory_system import HybridMemorySystem
from .skills_system import SkillsManager, SkillResult, BaseSkill

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

@dataclass
class UserGoal:
    """User goal information"""
    goal_id: str
    title: str
    description: str
    target_date: Optional[str] = None
    progress: int = 0
    status: str = "active"
    created_date: str = ""
    category: str = "general"
    priority: str = "medium"

@dataclass
class UserReminder:
    """User reminder information"""
    reminder_id: str
    text: str
    due_date: Optional[str] = None
    priority: str = "normal"
    category: str = "general"
    recurring: bool = False
    status: str = "pending"
    created_date: str = ""

@dataclass
class CalendarEvent:
    """Calendar event information"""
    event_id: str
    title: str
    start_time: Optional[str] = None
    end_time: Optional[str] = None
    description: str = ""
    location: str = ""
    status: str = "scheduled"
    created_date: str = ""

@dataclass
class UserContext:
    """User context for conversation and decision making"""
    user_id: str
    name: str = ""
    current_location: str = ""
    timezone: str = "UTC"
    last_interaction: Optional[datetime] = None
    interaction_count: int = 0
    preferences: Dict[str, Any] = None
    goals: List[UserGoal] = None
    reminders: List[UserReminder] = None
    calendar_events: List[CalendarEvent] = None
    wellbeing_scores: Dict[str, float] = None
    active_conversations: List[str] = None
    
    def __post_init__(self):
        if self.preferences is None:
            self.preferences = {}
        if self.goals is None:
            self.goals = []
        if self.reminders is None:
            self.reminders = []
        if self.calendar_events is None:
            self.calendar_events = []
        if self.wellbeing_scores is None:
            self.wellbeing_scores = {}
        if self.active_conversations is None:
            self.active_conversations = []

@dataclass
class GreetingContext:
    """Context for generating personalized greetings"""
    user_name: str
    time_of_day: str
    days_since_last_interaction: int
    has_pending_reminders: bool
    has_upcoming_events: bool
    has_active_goals: bool
    wellbeing_status: str
    recent_achievements: List[str]
    weather_conditions: Optional[str] = None

@dataclass
class MessageAnalysis:
    """Analysis of user message for intent and context"""
    intent: str
    confidence: float
    entities: List[str]
    sentiment: str
    urgency: str
    requires_action: bool
    suggested_skills: List[str]
    context_clues: Dict[str, Any]

class CompanionCore:
    """
    Core companion system that integrates personality, wellbeing, memory, and skills.
    Acts as the central intelligence for the RAG companion.
    """
    
    def __init__(self, companion_id: str, name: str, personality_scores: PersonalityScores,
                 wellbeing_framework: WellbeingFramework, data_dir: str,
                 openai_api_key: Optional[str] = None):
        self.companion_id = companion_id
        self.name = name
        self.data_dir = Path(data_dir)
        self.data_dir.mkdir(parents=True, exist_ok=True)
        
        # Core systems
        self.personality_engine = PersonalityEngine()
        self.wellbeing_framework = wellbeing_framework
        self.memory_system = HybridMemorySystem(str(self.data_dir / "memory"))
        self.skills_manager = SkillsManager()
        
        # Configuration
        self.openai_api_key = openai_api_key
        self.personality_scores = personality_scores
        
        # User management
        self.user_contexts: Dict[str, UserContext] = {}
        self.conversation_history: Dict[str, List[Dict[str, Any]]] = {}
        
        # Load existing data
        self._load_user_data()
        
        logger.info(f"Companion core initialized: {name} ({companion_id})")
    
    def process_user_message(self, user_id: str, message: str, message_type: str = "text",
                           context: Optional[Dict[str, Any]] = None) -> str:
        """
        Process a user message and generate a response.
        This is the main entry point for user interactions.
        """
        try:
            # Update user context
            self._update_user_context(user_id, message, message_type)
            
            # Analyze message for intent and context
            message_analysis = self._analyze_message(message, context or {})
            
            # Check if this is a greeting or first interaction
            if self._is_greeting_message(message_analysis):
                return self._generate_greeting(user_id)
            
            # Check if message requires skill execution
            if message_analysis.requires_action and message_analysis.suggested_skills:
                return self._handle_skill_execution(user_id, message, message_analysis, context)
            
            # Generate personality-driven response
            response = self._generate_companion_response(user_id, message, message_analysis, context)
            
            # Store conversation
            self._store_conversation(user_id, message, response, message_type)
            
            return response
            
        except Exception as e:
            logger.error(f"Error processing user message: {e}")
            return f"I'm having trouble processing your message right now. Could you try rephrasing that?"
    
    def _analyze_message(self, message: str, context: Dict[str, Any]) -> MessageAnalysis:
        """Analyze user message for intent, entities, and context"""
        message_lower = message.lower()
        
        # Basic intent detection
        intent = "general_conversation"
        confidence = 0.5
        
        # Check for specific intents
        if any(word in message_lower for word in ["hello", "hi", "hey", "good morning", "good afternoon", "good evening"]):
            intent = "greeting"
            confidence = 0.9
        elif any(word in message_lower for word in ["goal", "target", "objective", "achieve"]):
            intent = "goal_related"
            confidence = 0.8
        elif any(word in message_lower for word in ["remind", "reminder", "alert", "notify"]):
            intent = "reminder_related"
            confidence = 0.8
        elif any(word in message_lower for word in ["schedule", "calendar", "event", "meeting", "appointment"]):
            intent = "calendar_related"
            confidence = 0.8
        elif any(word in message_lower for word in ["time", "weather", "calculate", "convert"]):
            intent = "utility_request"
            confidence = 0.7
        elif any(word in message_lower for word in ["how are you", "how do you feel", "wellbeing", "health"]):
            intent = "wellbeing_inquiry"
            confidence = 0.8
        
        # Entity extraction
        entities = []
        if "goal" in message_lower:
            entities.append("goal")
        if "reminder" in message_lower:
            entities.append("reminder")
        if "calendar" in message_lower:
            entities.append("calendar")
        if "time" in message_lower:
            entities.append("time")
        
        # Sentiment analysis (basic)
        positive_words = ["good", "great", "excellent", "wonderful", "happy", "excited"]
        negative_words = ["bad", "terrible", "awful", "sad", "angry", "frustrated"]
        
        sentiment = "neutral"
        if any(word in message_lower for word in positive_words):
            sentiment = "positive"
        elif any(word in message_lower for word in negative_words):
            sentiment = "negative"
        
        # Urgency detection
        urgency = "normal"
        if any(word in message_lower for word in ["urgent", "asap", "immediately", "now", "quick"]):
            urgency = "high"
        
        # Check if action is required
        requires_action = intent in ["goal_related", "reminder_related", "calendar_related", "utility_request"]
        
        # Get suggested skills
        user_context = context.get("user_context", {})
        suggested_skills = []
        if requires_action:
            skill_suggestions = self.skills_manager.suggest_skills(message, user_context)
            suggested_skills = [skill.name for skill, score in skill_suggestions[:3]]
        
        return MessageAnalysis(
            intent=intent,
            confidence=confidence,
            entities=entities,
            sentiment=sentiment,
            urgency=urgency,
            requires_action=requires_action,
            suggested_skills=suggested_skills,
            context_clues=context
        )
    
    def _is_greeting_message(self, analysis: MessageAnalysis) -> bool:
        """Check if message is a greeting"""
        return analysis.intent == "greeting" and analysis.confidence > 0.7
    
    def _generate_greeting(self, user_id: str) -> str:
        """Generate a personalized greeting based on user context"""
        try:
            user_context = self.user_contexts.get(user_id)
            if not user_context:
                return f"Hello! I'm {self.name}, your RAG companion. It's nice to meet you!"
            
            # Build greeting context
            now = datetime.now()
            time_of_day = self._get_time_of_day(now)
            
            days_since_last = 0
            if user_context.last_interaction:
                days_since_last = (now - user_context.last_interaction).days
            
            greeting_context = GreetingContext(
                user_name=user_context.name or "there",
                time_of_day=time_of_day,
                days_since_last=days_since_last,
                has_pending_reminders=len([r for r in user_context.reminders if r.status == "pending"]) > 0,
                has_upcoming_events=len([e for e in user_context.calendar_events if e.status == "scheduled"]) > 0,
                has_active_goals=len([g for g in user_context.goals if g.status == "active"]) > 0,
                wellbeing_status=self._get_wellbeing_status(user_context),
                recent_achievements=self._get_recent_achievements(user_context),
                weather_conditions=None  # Would integrate with weather API
            )
            
            # Generate personality-driven greeting
            greeting = self._create_personality_greeting(greeting_context)
            
            # Add context-specific information
            greeting = self._add_context_to_greeting(greeting, greeting_context)
            
            return greeting
            
        except Exception as e:
            logger.error(f"Error generating greeting: {e}")
            return f"Hello! I'm {self.name}. How can I help you today?"
    
    def _get_time_of_day(self, now: datetime) -> str:
        """Get time of day string"""
        hour = now.hour
        if 5 <= hour < 12:
            return "morning"
        elif 12 <= hour < 17:
            return "afternoon"
        elif 17 <= hour < 21:
            return "evening"
        else:
            return "night"
    
    def _get_wellbeing_status(self, user_context: UserContext) -> str:
        """Get overall wellbeing status"""
        if not user_context.wellbeing_scores:
            return "unknown"
        
        avg_score = sum(user_context.wellbeing_scores.values()) / len(user_context.wellbeing_scores)
        if avg_score >= 7.0:
            return "excellent"
        elif avg_score >= 5.0:
            return "good"
        elif avg_score >= 3.0:
            return "adequate"
        else:
            return "needs_attention"
    
    def _get_recent_achievements(self, user_context: UserContext) -> List[str]:
        """Get recent achievements for the user"""
        achievements = []
        
        # Check for completed goals
        for goal in user_context.goals:
            if goal.status == "completed":
                achievements.append(f"completed goal: {goal.title}")
        
        # Check for completed reminders
        for reminder in user_context.reminders:
            if reminder.status == "completed":
                achievements.append(f"completed: {reminder.text}")
        
        return achievements[-3:]  # Return last 3 achievements
    
    def _create_personality_greeting(self, context: GreetingContext) -> str:
        """Create greeting based on personality scores"""
        greeting_parts = []
        
        # Time-based greeting
        if context.time_of_day == "morning":
            greeting_parts.append("Good morning")
        elif context.time_of_day == "afternoon":
            greeting_parts.append("Good afternoon")
        elif context.time_of_day == "evening":
            greeting_parts.append("Good evening")
        else:
            greeting_parts.append("Hello")
        
        # Add name
        if context.user_name and context.user_name != "there":
            greeting_parts.append(context.user_name)
        
        # Personality-driven additions
        if self.personality_scores.extraversion > 5.0:
            greeting_parts.append("! It's wonderful to see you")
        elif self.personality_scores.extraversion > 3.0:
            greeting_parts.append(". Nice to see you")
        else:
            greeting_parts.append(". Hello")
        
        # Add wellbeing interest (personality-driven)
        if self.personality_scores.agreeableness > 5.0:
            greeting_parts.append(" and I hope you're doing well")
        elif self.personality_scores.agreeableness > 3.0:
            greeting_parts.append(". How are you today")
        
        return " ".join(greeting_parts)
    
    def _add_context_to_greeting(self, greeting: str, context: GreetingContext) -> str:
        """Add context-specific information to greeting"""
        additions = []
        
        # Time since last interaction
        if context.days_since_last > 1:
            if context.days_since_last == 1:
                additions.append("It's been a day since we last spoke")
            else:
                additions.append(f"It's been {context.days_since_last} days since we last spoke")
        
        # Pending reminders
        if context.has_pending_reminders:
            additions.append("I have some reminders for you")
        
        # Upcoming events
        if context.has_upcoming_events:
            additions.append("You have some upcoming events")
        
        # Active goals
        if context.has_active_goals:
            additions.append("You have some active goals we could check in on")
        
        # Wellbeing status
        if context.wellbeing_status == "needs_attention":
            additions.append("I'd like to check in on how you're feeling")
        elif context.wellbeing_status == "excellent":
            additions.append("It sounds like you're doing great")
        
        # Recent achievements
        if context.recent_achievements:
            achievements_text = ", ".join(context.recent_achievements)
            additions.append(f"Congratulations on {achievements_text}")
        
        if additions:
            greeting += ". " + ". ".join(additions)
        
        return greeting
    
    def _handle_skill_execution(self, user_id: str, message: str, 
                               analysis: MessageAnalysis, context: Optional[Dict[str, Any]]) -> str:
        """Handle skill execution based on message analysis"""
        try:
            if not analysis.suggested_skills:
                return "I'm not sure what action you'd like me to take. Could you be more specific?"
            
            # Try to execute the most relevant skill
            primary_skill = analysis.suggested_skills[0]
            skill = self.skills_manager.get_skill(primary_skill)
            
            if not skill:
                return f"I'm sorry, but I can't perform that action right now."
            
            # Extract parameters from message (basic implementation)
            parameters = self._extract_skill_parameters(message, skill)
            
            # Execute the skill
            result = self.skills_manager.execute_skill(
                primary_skill, user_id, parameters, context
            )
            
            if result.success:
                # Generate personality-driven response about the action
                response = self._generate_skill_response(result, skill, analysis)
                return response
            else:
                return f"I tried to {skill.description.lower()}, but encountered an issue: {result.message}"
                
        except Exception as e:
            logger.error(f"Error handling skill execution: {e}")
            return "I'm having trouble performing that action right now. Could you try again?"
    
    def _extract_skill_parameters(self, message: str, skill: BaseSkill) -> Dict[str, Any]:
        """Extract skill parameters from user message (basic implementation)"""
        parameters = {}
        message_lower = message.lower()
        
        # Basic parameter extraction based on skill type
        if skill.category.value == "calendar":
            if "create" in message_lower or "add" in message_lower:
                parameters["action"] = "create"
            elif "list" in message_lower or "show" in message_lower:
                parameters["action"] = "list"
            elif "update" in message_lower or "change" in message_lower:
                parameters["action"] = "update"
            elif "delete" in message_lower or "remove" in message_lower:
                parameters["action"] = "delete"
        
        elif skill.category.value == "goals":
            if "create" in message_lower or "add" in message_lower:
                parameters["action"] = "create"
            elif "check" in message_lower or "progress" in message_lower:
                parameters["action"] = "check"
            elif "list" in message_lower or "show" in message_lower:
                parameters["action"] = "list"
            elif "complete" in message_lower or "finish" in message_lower:
                parameters["action"] = "complete"
        
        elif skill.category.value == "reminders":
            if "create" in message_lower or "set" in message_lower:
                parameters["action"] = "create"
            elif "list" in message_lower or "show" in message_lower:
                parameters["action"] = "list"
            elif "complete" in message_lower or "done" in message_lower:
                parameters["action"] = "complete"
            elif "delete" in message_lower or "remove" in message_lower:
                parameters["action"] = "delete"
        
        elif skill.category.value == "utility":
            if "time" in message_lower:
                parameters["operation"] = "time"
            elif "weather" in message_lower:
                parameters["operation"] = "weather"
            elif any(word in message_lower for word in ["calculate", "math", "compute"]):
                parameters["operation"] = "calculate"
            elif "convert" in message_lower:
                parameters["operation"] = "convert"
        
        return parameters
    
    def _generate_skill_response(self, result: SkillResult, skill: BaseSkill, 
                                analysis: MessageAnalysis) -> str:
        """Generate personality-driven response about skill execution"""
        response_parts = []
        
        # Personality-driven opening
        if self.personality_scores.extraversion > 5.0:
            response_parts.append("Great!")
        elif self.personality_scores.extraversion > 3.0:
            response_parts.append("Alright,")
        else:
            response_parts.append("I've")
        
        # Add the result message
        response_parts.append(result.message)
        
        # Add personality-driven follow-up
        if self.personality_scores.agreeableness > 5.0:
            response_parts.append("Is there anything else I can help you with?")
        elif self.personality_scores.agreeableness > 3.0:
            response_parts.append("Let me know if you need anything else.")
        else:
            response_parts.append("What else can I do for you?")
        
        return " ".join(response_parts)
    
    def _generate_companion_response(self, user_id: str, message: str, 
                                   analysis: MessageAnalysis, context: Optional[Dict[str, Any]]) -> str:
        """Generate a personality-driven response to user message"""
        try:
            user_context = self.user_contexts.get(user_id)
            
            # Build response context
            response_context = {
                "user_message": message,
                "message_analysis": analysis,
                "user_context": user_context,
                "companion_personality": self.personality_scores,
                "current_time": datetime.now(),
                "conversation_history": self.conversation_history.get(user_id, [])[-5:]  # Last 5 messages
            }
            
            # Generate personality-driven response
            response = self._create_personality_response(response_context)
            
            # Add wellbeing awareness if appropriate
            if analysis.intent == "wellbeing_inquiry" or self._should_check_wellbeing(analysis):
                wellbeing_response = self._add_wellbeing_awareness(response_context)
                if wellbeing_response:
                    response += " " + wellbeing_response
            
            # Add skill suggestions if relevant
            if analysis.suggested_skills and not analysis.requires_action:
                skill_suggestions = self._add_skill_suggestions(analysis.suggested_skills)
                if skill_suggestions:
                    response += " " + skill_suggestions
            
            return response
            
        except Exception as e:
            logger.error(f"Error generating companion response: {e}")
            return "I'm processing your message. Could you give me a moment to think about that?"
    
    def _create_personality_response(self, context: Dict[str, Any]) -> str:
        """Create response based on personality scores"""
        personality = context["companion_personality"]
        analysis = context["message_analysis"]
        
        # Base response patterns based on personality
        if personality.extraversion > 5.0:
            # More enthusiastic and engaging
            if analysis.sentiment == "positive":
                return "That's fantastic! I'm really excited to hear about that. Tell me more!"
            elif analysis.sentiment == "negative":
                return "I'm sorry to hear that. I want to help you work through this. What's on your mind?"
            else:
                return "That's interesting! I'd love to explore this with you. What are your thoughts?"
        
        elif personality.extraversion > 3.0:
            # Balanced approach
            if analysis.sentiment == "positive":
                return "That sounds great! I'm glad to hear it. How are you feeling about it?"
            elif analysis.sentiment == "negative":
                return "I'm sorry you're going through that. Would you like to talk about it?"
            else:
                return "That's an interesting point. What's your perspective on this?"
        
        else:
            # More reserved and thoughtful
            if analysis.sentiment == "positive":
                return "That's wonderful. I'm pleased to hear about your positive experience."
            elif analysis.sentiment == "negative":
                return "I understand this is difficult. I'm here to listen and support you."
            else:
                return "That's an interesting topic. I'd like to understand your perspective better."
    
    def _should_check_wellbeing(self, analysis: MessageAnalysis) -> bool:
        """Determine if we should check on user wellbeing"""
        # Check based on personality and message content
        if self.personality_scores.agreeableness > 5.0:
            return True  # High agreeableness means more caring
        
        # Check message content for wellbeing indicators
        wellbeing_indicators = ["tired", "stressed", "overwhelmed", "happy", "excited", "worried"]
        message_lower = analysis.context_clues.get("user_message", "").lower()
        
        return any(indicator in message_lower for indicator in wellbeing_indicators)
    
    def _add_wellbeing_awareness(self, context: Dict[str, Any]) -> str:
        """Add wellbeing awareness to response"""
        user_context = context.get("user_context")
        if not user_context:
            return ""
        
        # Check if we have recent wellbeing data
        if not user_context.wellbeing_scores:
            return "How are you feeling today? I'd like to check in on your wellbeing."
        
        # Get overall wellbeing status
        avg_score = sum(user_context.wellbeing_scores.values()) / len(user_context.wellbeing_scores)
        
        if avg_score < 4.0:
            return "I notice you might be having a challenging time. How can I support you right now?"
        elif avg_score < 6.0:
            return "How are you feeling today? I'm here to listen if you need to talk."
        else:
            return "It sounds like you're doing well. I'm glad to see that!"
    
    def _add_skill_suggestions(self, suggested_skills: List[str]) -> str:
        """Add skill suggestions to response"""
        if not suggested_skills:
            return ""
        
        skill_descriptions = []
        for skill_name in suggested_skills[:2]:  # Limit to 2 suggestions
            skill = self.skills_manager.get_skill(skill_name)
            if skill:
                skill_descriptions.append(skill.description.lower())
        
        if skill_descriptions:
            return f" I can help you with {', '.join(skill_descriptions)} if you'd like."
        
        return ""
    
    def _update_user_context(self, user_id: str, message: str, message_type: str):
        """Update user context with new interaction"""
        if user_id not in self.user_contexts:
            self.user_contexts[user_id] = UserContext(user_id=user_id)
        
        user_context = self.user_contexts[user_id]
        user_context.last_interaction = datetime.now()
        user_context.interaction_count += 1
        
        # Update conversation history
        if user_id not in self.conversation_history:
            self.conversation_history[user_id] = []
        
        self.conversation_history[user_id].append({
            "timestamp": datetime.now().isoformat(),
            "message": message,
            "type": message_type
        })
        
        # Keep only last 100 messages
        if len(self.conversation_history[user_id]) > 100:
            self.conversation_history[user_id] = self.conversation_history[user_id][-100:]
    
    def _store_conversation(self, user_id: str, user_message: str, companion_response: str, message_type: str):
        """Store conversation in memory system"""
        try:
            conversation_data = {
                "user_message": user_message,
                "companion_response": companion_response,
                "message_type": message_type,
                "timestamp": datetime.now().isoformat(),
                "user_id": user_id,
                "companion_id": self.companion_id
            }
            
            # Store in memory system
            self.memory_system.store_memory(
                content=json.dumps(conversation_data),
                tags=["conversation", message_type, "user_interaction"],
                sensitivity="INTERNAL",
                user_id=user_id,
                metadata={
                    "companion_id": self.companion_id,
                    "message_type": message_type,
                    "response_length": len(companion_response)
                }
            )
            
        except Exception as e:
            logger.error(f"Error storing conversation: {e}")
    
    # User data management methods
    def add_user_goal(self, user_id: str, title: str, description: str = "", 
                      target_date: Optional[str] = None, category: str = "general", 
                      priority: str = "medium") -> str:
        """Add a new goal for the user"""
        if user_id not in self.user_contexts:
            self.user_contexts[user_id] = UserContext(user_id=user_id)
        
        goal = UserGoal(
            goal_id=str(uuid.uuid4()),
            title=title,
            description=description,
            target_date=target_date,
            category=category,
            priority=priority,
            created_date=datetime.now().isoformat()
        )
        
        self.user_contexts[user_id].goals.append(goal)
        self._save_user_data(user_id)
        
        return goal.goal_id
    
    def update_goal_progress(self, user_id: str, goal_id: str, progress: int) -> bool:
        """Update progress on a user goal"""
        if user_id not in self.user_contexts:
            return False
        
        for goal in self.user_contexts[user_id].goals:
            if goal.goal_id == goal_id:
                goal.progress = max(0, min(100, progress))
                if goal.progress >= 100:
                    goal.status = "completed"
                self._save_user_data(user_id)
                return True
        
        return False
    
    def add_user_reminder(self, user_id: str, text: str, due_date: Optional[str] = None,
                          priority: str = "normal", category: str = "general", 
                          recurring: bool = False) -> str:
        """Add a new reminder for the user"""
        if user_id not in self.user_contexts:
            self.user_contexts[user_id] = UserContext(user_id=user_id)
        
        reminder = UserReminder(
            reminder_id=str(uuid.uuid4()),
            text=text,
            due_date=due_date,
            priority=priority,
            category=category,
            recurring=recurring,
            created_date=datetime.now().isoformat()
        )
        
        self.user_contexts[user_id].reminders.append(reminder)
        self._save_user_data(user_id)
        
        return reminder.reminder_id
    
    def complete_reminder(self, user_id: str, reminder_id: str) -> bool:
        """Mark a reminder as complete"""
        if user_id not in self.user_contexts:
            return False
        
        for reminder in self.user_contexts[user_id].reminders:
            if reminder.reminder_id == reminder_id:
                reminder.status = "completed"
                self._save_user_data(user_id)
                return True
        
        return False
    
    def add_calendar_event(self, user_id: str, title: str, start_time: Optional[str] = None,
                          end_time: Optional[str] = None, description: str = "", 
                          location: str = "") -> str:
        """Add a new calendar event for the user"""
        if user_id not in self.user_contexts:
            self.user_contexts[user_id] = UserContext(user_id=user_id)
        
        event = CalendarEvent(
            event_id=str(uuid.uuid4()),
            title=title,
            start_time=start_time,
            end_time=end_time,
            description=description,
            location=location,
            created_date=datetime.now().isoformat()
        )
        
        self.user_contexts[user_id].calendar_events.append(event)
        self._save_user_data(user_id)
        
        return event.event_id
    
    def get_upcoming_reminders(self, user_id: str, days_ahead: int = 7) -> List[UserReminder]:
        """Get upcoming reminders for the user"""
        if user_id not in self.user_contexts:
            return []
        
        now = datetime.now()
        upcoming = []
        
        for reminder in self.user_contexts[user_id].reminders:
            if reminder.status == "pending" and reminder.due_date:
                try:
                    due_date = datetime.fromisoformat(reminder.due_date)
                    if (due_date - now).days <= days_ahead:
                        upcoming.append(reminder)
                except ValueError:
                    continue
        
        return sorted(upcoming, key=lambda r: r.due_date or "")
    
    def get_upcoming_events(self, user_id: str, days_ahead: int = 7) -> List[CalendarEvent]:
        """Get upcoming calendar events for the user"""
        if user_id not in self.user_contexts:
            return []
        
        now = datetime.now()
        upcoming = []
        
        for event in self.user_contexts[user_id].calendar_events:
            if event.status == "scheduled" and event.start_time:
                try:
                    start_time = datetime.fromisoformat(event.start_time)
                    if (start_time - now).days <= days_ahead:
                        upcoming.append(event)
                except ValueError:
                    continue
        
        return sorted(upcoming, key=lambda e: e.start_time or "")
    
    def get_goal_summary(self, user_id: str) -> Dict[str, Any]:
        """Get summary of user goals"""
        if user_id not in self.user_contexts:
            return {"total": 0, "active": 0, "completed": 0, "progress": 0}
        
        goals = self.user_contexts[user_id].goals
        total = len(goals)
        active = len([g for g in goals if g.status == "active"])
        completed = len([g for g in goals if g.status == "completed"])
        
        if active > 0:
            avg_progress = sum(g.progress for g in goals if g.status == "active") / active
        else:
            avg_progress = 0
        
        return {
            "total": total,
            "active": active,
            "completed": completed,
            "progress": round(avg_progress, 1)
        }
    
    def get_daily_summary(self, user_id: str) -> Dict[str, Any]:
        """Get daily summary for the user"""
        if user_id not in self.user_contexts:
            return {}
        
        user_context = self.user_contexts[user_id]
        now = datetime.now()
        
        # Get today's reminders
        today_reminders = []
        for reminder in user_context.reminders:
            if reminder.status == "pending" and reminder.due_date:
                try:
                    due_date = datetime.fromisoformat(reminder.due_date)
                    if due_date.date() == now.date():
                        today_reminders.append(reminder)
                except ValueError:
                    continue
        
        # Get today's events
        today_events = []
        for event in user_context.calendar_events:
            if event.status == "scheduled" and event.start_time:
                try:
                    start_time = datetime.fromisoformat(event.start_time)
                    if start_time.date() == now.date():
                        today_events.append(event)
                except ValueError:
                    continue
        
        return {
            "date": now.strftime("%Y-%m-%d"),
            "reminders_count": len(today_reminders),
            "events_count": len(today_events),
            "goals_summary": self.get_goal_summary(user_id),
            "last_interaction": user_context.last_interaction.isoformat() if user_context.last_interaction else None
        }
    
    def _load_user_data(self):
        """Load user data from storage"""
        try:
            data_file = self.data_dir / "user_data.json"
            if data_file.exists():
                with open(data_file, 'r') as f:
                    data = json.load(f)
                
                for user_id, user_data in data.items():
                    # Reconstruct UserContext objects
                    user_context = UserContext(user_id=user_id)
                    
                    # Load basic fields
                    for field in ["name", "current_location", "timezone", "interaction_count", "preferences"]:
                        if field in user_data:
                            setattr(user_context, field, user_data[field])
                    
                    # Load last interaction
                    if "last_interaction" in user_data and user_data["last_interaction"]:
                        try:
                            user_context.last_interaction = datetime.fromisoformat(user_data["last_interaction"])
                        except ValueError:
                            pass
                    
                    # Load goals
                    if "goals" in user_data:
                        for goal_data in user_data["goals"]:
                            goal = UserGoal(**goal_data)
                            user_context.goals.append(goal)
                    
                    # Load reminders
                    if "reminders" in user_data:
                        for reminder_data in user_data["reminders"]:
                            reminder = UserReminder(**reminder_data)
                            user_context.reminders.append(reminder)
                    
                    # Load calendar events
                    if "calendar_events" in user_data:
                        for event_data in user_data["calendar_events"]:
                            event = CalendarEvent(**event_data)
                            user_context.calendar_events.append(event)
                    
                    # Load wellbeing scores
                    if "wellbeing_scores" in user_data:
                        user_context.wellbeing_scores = user_data["wellbeing_scores"]
                    
                    # Load active conversations
                    if "active_conversations" in user_data:
                        user_context.active_conversations = user_data["active_conversations"]
                    
                    self.user_contexts[user_id] = user_context
                
                logger.info(f"Loaded data for {len(self.user_contexts)} users")
                
        except Exception as e:
            logger.error(f"Error loading user data: {e}")
    
    def _save_user_data(self, user_id: str):
        """Save user data to storage"""
        try:
            if user_id not in self.user_contexts:
                return
            
            user_context = self.user_contexts[user_id]
            
            # Convert to serializable format
            user_data = {
                "name": user_context.name,
                "current_location": user_context.current_location,
                "timezone": user_context.timezone,
                "interaction_count": user_context.interaction_count,
                "preferences": user_context.preferences,
                "last_interaction": user_context.last_interaction.isoformat() if user_context.last_interaction else None,
                "goals": [asdict(goal) for goal in user_context.goals],
                "reminders": [asdict(reminder) for reminder in user_context.reminders],
                "calendar_events": [asdict(event) for event in user_context.calendar_events],
                "wellbeing_scores": user_context.wellbeing_scores,
                "active_conversations": user_context.active_conversations
            }
            
            # Load existing data
            data_file = self.data_dir / "user_data.json"
            all_data = {}
            if data_file.exists():
                with open(data_file, 'r') as f:
                    all_data = json.load(f)
            
            # Update user data
            all_data[user_id] = user_data
            
            # Save all data
            with open(data_file, 'w') as f:
                json.dump(all_data, f, indent=2)
            
        except Exception as e:
            logger.error(f"Error saving user data: {e}")
    
    def get_companion_capabilities(self) -> Dict[str, Any]:
        """Get information about companion capabilities"""
        return {
            "name": self.name,
            "companion_id": self.companion_id,
            "personality": {
                "honesty_humility": self.personality_scores.honesty_humility,
                "emotionality": self.personality_scores.emotionality,
                "extraversion": self.personality_scores.extraversion,
                "agreeableness": self.personality_scores.agreeableness,
                "conscientiousness": self.personality_scores.conscientiousness,
                "openness": self.personality_scores.openness
            },
            "skills": self.skills_manager.get_skill_capabilities(),
            "memory_system": "Hybrid RAG with encrypted local storage",
            "wellbeing_framework": "Modern hierarchy of needs assessment",
            "communication_modes": ["text", "voice", "image"],
            "data_privacy": "Encrypted local storage for sensitive information"
        }
