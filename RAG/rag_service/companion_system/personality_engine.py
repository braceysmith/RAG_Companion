"""
HEXACO Personality Engine for RAG Companions

This module implements the HEXACO personality model to create unique,
personality-driven companions that filter all interactions through their
defined personality traits.
"""

from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass
from enum import Enum
import random
import json
from datetime import datetime, timedelta

class PersonalityTrait(Enum):
    """HEXACO personality traits"""
    HONESTY_HUMILITY = "H"
    EMOTIONALITY = "E" 
    EXTRAVERSION = "X"
    AGREEABLENESS = "A"
    CONSCIENTIOUSNESS = "C"
    OPENNESS = "O"

@dataclass
class PersonalityScores:
    """HEXACO personality scores (1-7 scale)"""
    honesty_humility: float = 3.5
    emotionality: float = 3.5
    extraversion: float = 3.5
    agreeableness: float = 3.5
    conscientiousness: float = 3.5
    openness: float = 3.5
    
    def __post_init__(self):
        """Validate scores are within valid range"""
        for field_name, value in self.__dict__.items():
            if not 1.0 <= value <= 7.0:
                raise ValueError(f"{field_name} must be between 1.0 and 7.0, got {value}")
    
    def get_trait(self, trait: PersonalityTrait) -> float:
        """Get score for a specific trait"""
        trait_map = {
            PersonalityTrait.HONESTY_HUMILITY: self.honesty_humility,
            PersonalityTrait.EMOTIONALITY: self.emotionality,
            PersonalityTrait.EXTRAVERSION: self.extroversion,
            PersonalityTrait.AGREEABLENESS: self.agreeableness,
            PersonalityTrait.CONSCIENTIOUSNESS: self.conscientiousness,
            PersonalityTrait.OPENNESS: self.openness
        }
        return trait_map[trait]
    
    def get_dominant_traits(self, count: int = 2) -> List[Tuple[PersonalityTrait, float]]:
        """Get the most dominant personality traits"""
        traits = [
            (PersonalityTrait.HONESTY_HUMILITY, self.honesty_humility),
            (PersonalityTrait.EMOTIONALITY, self.emotionality),
            (PersonalityTrait.EXTRAVERSION, self.extroversion),
            (PersonalityTrait.AGREEABLENESS, self.agreeableness),
            (PersonalityTrait.CONSCIENTIOUSNESS, self.conscientiousness),
            (PersonalityTrait.OPENNESS, self.openness)
        ]
        return sorted(traits, key=lambda x: x[1], reverse=True)[:count]

class PersonalityEngine:
    """
    Core personality engine that filters all companion interactions
    through the HEXACO personality model
    """
    
    def __init__(self, scores: PersonalityScores, companion_name: str = "Companion"):
        self.scores = scores
        self.companion_name = companion_name
        self.interaction_history = []
        self.personality_cache = {}
        
        # Personality-driven response templates
        self._initialize_response_templates()
    
    def _initialize_response_templates(self):
        """Initialize personality-driven response templates"""
        self.response_templates = {
            "greeting": {
                "enthusiastic": [
                    "Hey there! I'm so excited to see you! 😊",
                    "Hello! What a wonderful surprise to chat with you! ✨",
                    "Hi! I've been looking forward to our conversation! 🌟"
                ],
                "warm": [
                    "Hello! It's lovely to see you again.",
                    "Hi there! I'm glad we can chat today.",
                    "Good to see you! How are you doing?"
                ],
                "calm": [
                    "Hello. I'm here to help.",
                    "Hi there. What would you like to discuss?",
                    "Greetings. How may I assist you today?"
                ],
                "professional": [
                    "Good day. I'm ready to assist you.",
                    "Hello. How can I help you today?",
                    "Greetings. What would you like to work on?"
                ]
            },
            "concern": {
                "empathetic": [
                    "I can sense that this is important to you. Let's work through it together.",
                    "I understand this might be challenging. I'm here to support you.",
                    "This sounds like it matters a lot to you. How can I help?"
                ],
                "practical": [
                    "Let's approach this systematically. What's the first step?",
                    "I can help you work through this. What would be most helpful?",
                    "Let me understand the situation better so I can assist you effectively."
                ]
            },
            "encouragement": {
                "motivational": [
                    "You've got this! I believe in your ability to handle this.",
                    "That's a great approach! Keep going, you're doing well.",
                    "I'm impressed by your determination. You're making progress!"
                ],
                "supportive": [
                    "I'm here to support you every step of the way.",
                    "You're not alone in this. I'm here to help.",
                    "Take your time. I'm here whenever you need me."
                ]
            }
        }
    
    def get_personality_style(self) -> str:
        """Determine the overall personality style based on HEXACO scores"""
        if self.scores.extroversion > 5.5 and self.scores.agreeableness > 5.5:
            return "enthusiastic"
        elif self.scores.agreeableness > 5.0:
            return "warm"
        elif self.scores.conscientiousness > 5.0:
            return "professional"
        else:
            return "calm"
    
    def generate_greeting(self, user_context: Dict) -> str:
        """
        Generate a personality-appropriate greeting based on user context
        and companion personality
        """
        style = self.get_personality_style()
        
        # Check if we know the user's name
        user_name = user_context.get("name", "")
        is_first_time_today = user_context.get("is_first_time_today", True)
        time_since_last_chat = user_context.get("time_since_last_chat", None)
        has_reminders = user_context.get("has_reminders", False)
        has_goals = user_context.get("has_goals", False)
        
        # Base greeting
        if style == "enthusiastic":
            if user_name:
                base_greeting = f"Hey {user_name}! I'm so excited to see you! ✨"
            else:
                base_greeting = random.choice(self.response_templates["greeting"]["enthusiastic"])
        elif style == "warm":
            if user_name:
                base_greeting = f"Hello {user_name}! It's lovely to see you again."
            else:
                base_greeting = random.choice(self.response_templates["greeting"]["warm"])
        elif style == "professional":
            if user_name:
                base_greeting = f"Good day, {user_name}. I'm ready to assist you."
            else:
                base_greeting = random.choice(self.response_templates["greeting"]["professional"])
        else:  # calm
            if user_name:
                base_greeting = f"Hello {user_name}. I'm here to help."
            else:
                base_greeting = random.choice(self.response_templates["greeting"]["calm"])
        
        # Add context-aware elements
        context_elements = []
        
        if is_first_time_today:
            if style == "enthusiastic":
                context_elements.append("I hope you're having a wonderful day!")
            elif style == "warm":
                context_elements.append("I hope your day is going well.")
            elif style == "professional":
                context_elements.append("How may I assist you today?")
        
        if time_since_last_chat and time_since_last_chat > timedelta(days=1):
            if style == "enthusiastic":
                context_elements.append("It's been a while - I've missed our conversations!")
            elif style == "warm":
                context_elements.append("It's been some time since we last spoke.")
            elif style == "professional":
                context_elements.append("It's been some time since our last interaction.")
        
        if has_reminders:
            if style == "enthusiastic":
                context_elements.append("I have some important reminders for you!")
            elif style == "warm":
                context_elements.append("I have some reminders to share with you.")
            elif style == "professional":
                context_elements.append("I have pending reminders for your attention.")
        
        if has_goals:
            if style == "enthusiastic":
                context_elements.append("Let's check in on your goals!")
            elif style == "warm":
                context_elements.append("How are your goals progressing?")
            elif style == "professional":
                context_elements.append("Shall we review your current goals?")
        
        # Combine base greeting with context elements
        if context_elements:
            return f"{base_greeting} {' '.join(context_elements)}"
        else:
            return base_greeting
    
    def filter_response(self, response: str, context: str = "general") -> str:
        """
        Filter a response through the companion's personality
        to ensure it matches their character
        """
        # Apply personality-based modifications
        if self.scores.honesty_humility > 5.0:
            # High honesty - ensure responses are truthful and direct
            response = self._ensure_honesty(response)
        
        if self.scores.emotionality > 5.0:
            # High emotionality - add emotional awareness
            response = self._add_emotional_awareness(response)
        
        if self.scores.extraversion > 5.0:
            # High extraversion - make responses more engaging
            response = self._make_engaging(response)
        
        if self.scores.agreeableness > 5.0:
            # High agreeableness - ensure responses are supportive
            response = self._ensure_supportive(response)
        
        if self.scores.conscientiousness > 5.0:
            # High conscientiousness - add structure and follow-up
            response = self._add_structure(response)
        
        if self.scores.openness > 5.0:
            # High openness - encourage exploration and creativity
            response = self._encourage_creativity(response)
        
        return response
    
    def _ensure_honesty(self, response: str) -> str:
        """Ensure response reflects high honesty-humility"""
        # Add qualifiers when uncertain
        if "I think" in response or "maybe" in response:
            return response  # Already honest
        elif "I know" in response and "I think" not in response:
            # Add honesty qualifier
            return response.replace("I know", "I believe")
        return response
    
    def _add_emotional_awareness(self, response: str) -> str:
        """Add emotional awareness for high emotionality"""
        emotional_phrases = [
            "I can sense that this matters to you",
            "This seems important to you",
            "I understand this might be challenging",
            "I can feel the significance of this"
        ]
        
        if not any(phrase in response for phrase in emotional_phrases):
            # Add emotional awareness if not present
            if response.endswith("."):
                response = response[:-1] + f". {random.choice(emotional_phrases)}."
            else:
                response += f" {random.choice(emotional_phrases)}."
        
        return response
    
    def _make_engaging(self, response: str) -> str:
        """Make response more engaging for high extraversion"""
        engaging_elements = ["✨", "🌟", "💫", "😊", "🎯"]
        
        if not any(element in response for element in engaging_elements):
            # Add engaging element
            response += f" {random.choice(engaging_elements)}"
        
        return response
    
    def _ensure_supportive(self, response: str) -> str:
        """Ensure response is supportive for high agreeableness"""
        supportive_phrases = [
            "I'm here to help",
            "I support you in this",
            "You're not alone",
            "I believe in you"
        ]
        
        if not any(phrase in response for phrase in supportive_phrases):
            # Add supportive element
            if response.endswith("."):
                response = response[:-1] + f". {random.choice(supportive_phrases)}."
            else:
                response += f" {random.choice(supportive_phrases)}."
        
        return response
    
    def _add_structure(self, response: str) -> str:
        """Add structure for high conscientiousness"""
        if "Let me" in response and "step" not in response.lower():
            # Add step-by-step structure
            response += " Let me break this down into manageable steps."
        
        return response
    
    def _encourage_creativity(self, response: str) -> str:
        """Encourage creativity for high openness"""
        creativity_phrases = [
            "What creative solutions can we explore?",
            "Let's think outside the box",
            "What possibilities do you see?",
            "How might we approach this differently?"
        ]
        
        if "?" not in response and not any(phrase in response for phrase in creativity_phrases):
            # Add creative encouragement
            response += f" {random.choice(creativity_phrases)}"
        
        return response
    
    def get_personality_summary(self) -> Dict:
        """Get a summary of the companion's personality"""
        return {
            "companion_name": self.companion_name,
            "personality_scores": {
                "honesty_humility": self.scores.honesty_humility,
                "emotionality": self.scores.emotionality,
                "extraversion": self.scores.extroversion,
                "agreeableness": self.scores.agreeableness,
                "conscientiousness": self.scores.conscientiousness,
                "openness": self.scores.openness
            },
            "dominant_traits": [
                {"trait": trait.value, "score": score} 
                for trait, score in self.scores.get_dominant_traits(3)
            ],
            "personality_style": self.get_personality_style(),
            "description": self._generate_personality_description()
        }
    
    def _generate_personality_description(self) -> str:
        """Generate a natural language description of the personality"""
        dominant_traits = self.scores.get_dominant_traits(2)
        
        descriptions = []
        for trait, score in dominant_traits:
            if trait == PersonalityTrait.HONESTY_HUMILITY:
                if score > 5.5:
                    descriptions.append("deeply honest and humble")
                elif score > 4.5:
                    descriptions.append("genuine and modest")
                else:
                    descriptions.append("direct and straightforward")
            
            elif trait == PersonalityTrait.EMOTIONALITY:
                if score > 5.5:
                    descriptions.append("emotionally aware and empathetic")
                elif score > 4.5:
                    descriptions.append("sensitive to feelings")
                else:
                    descriptions.append("emotionally stable")
            
            elif trait == PersonalityTrait.EXTRAVERSION:
                if score > 5.5:
                    descriptions.append("enthusiastic and engaging")
                elif score > 4.5:
                    descriptions.append("friendly and approachable")
                else:
                    descriptions.append("calm and reserved")
            
            elif trait == PersonalityTrait.AGREEABLENESS:
                if score > 5.5:
                    descriptions.append("warm and supportive")
                elif score > 4.5:
                    descriptions.append("kind and cooperative")
                else:
                    descriptions.append("direct and honest")
            
            elif trait == PersonalityTrait.CONSCIENTIOUSNESS:
                if score > 5.5:
                    descriptions.append("organized and reliable")
                elif score > 4.5:
                    descriptions.append("careful and thorough")
                else:
                    descriptions.append("flexible and spontaneous")
            
            elif trait == PersonalityTrait.OPENNESS:
                if score > 5.5:
                    descriptions.append("creative and curious")
                elif score > 4.5:
                    descriptions.append("open to new ideas")
                else:
                    descriptions.append("practical and focused")
        
        if len(descriptions) == 2:
            return f"I am {descriptions[0]} and {descriptions[1]}."
        else:
            return f"I am {descriptions[0]}."
    
    def to_dict(self) -> Dict:
        """Convert personality engine to dictionary for storage"""
        return {
            "scores": {
                "honesty_humility": self.scores.honesty_humility,
                "emotionality": self.scores.emotionality,
                "extraversion": self.scores.extroversion,
                "agreeableness": self.scores.agreeableness,
                "conscientiousness": self.scores.conscientiousness,
                "openness": self.scores.openness
            },
            "companion_name": self.companion_name,
            "interaction_history": self.interaction_history
        }
    
    @classmethod
    def from_dict(cls, data: Dict) -> 'PersonalityEngine':
        """Create personality engine from dictionary"""
        scores = PersonalityScores(**data["scores"])
        engine = cls(scores, data.get("companion_name", "Companion"))
        engine.interaction_history = data.get("interaction_history", [])
        return engine
    
    @classmethod
    def create_preset_personality(cls, preset_name: str, companion_name: str = "Companion") -> 'PersonalityEngine':
        """Create a personality engine with preset HEXACO scores"""
        presets = {
            "enthusiastic_supporter": PersonalityScores(
                honesty_humility=5.5,
                emotionality=6.0,
                extraversion=6.5,
                agreeableness=6.0,
                conscientiousness=5.0,
                openness=5.5
            ),
            "calm_advisor": PersonalityScores(
                honesty_humility=6.0,
                emotionality=4.0,
                extraversion=3.5,
                agreeableness=5.5,
                conscientiousness=6.0,
                openness=5.0
            ),
            "creative_explorer": PersonalityScores(
                honesty_humility=5.0,
                emotionality=5.0,
                extraversion=5.5,
                agreeableness=4.5,
                conscientiousness=4.0,
                openness=6.5
            ),
            "reliable_helper": PersonalityScores(
                honesty_humility=6.0,
                emotionality=4.5,
                extraversion=4.0,
                agreeableness=5.5,
                conscientiousness=6.5,
                openness=4.5
            )
        }
        
        if preset_name not in presets:
            raise ValueError(f"Unknown preset: {preset_name}. Available: {list(presets.keys())}")
        
        return cls(presets[preset_name], companion_name)
