"""
Hierarchy of Needs Framework for RAG Companions

This module implements Maslow's hierarchy of needs adapted for modern digital well-being,
guiding companion behavior and memory organization.
"""

from typing import Dict, List, Optional, Tuple
from enum import Enum
from datetime import datetime, timedelta
import json

class NeedLevel(Enum):
    """Hierarchy of needs levels"""
    ESSENTIAL = 1
    SAFETY = 2
    DIGITAL_WELLBEING = 3
    LOVE_BELONGING = 4
    ESTEEM = 5
    SELF_ACTUALIZATION = 6

class NeedCategory(Enum):
    """Specific need categories within each level"""
    # Essential (Level 1)
    FOOD = "food"
    WATER = "water"
    REST = "rest"
    MOVEMENT = "movement"
    SHELTER = "shelter"
    HEALTH = "health"
    
    # Safety (Level 2)
    PHYSICAL_SAFETY = "physical_safety"
    FINANCIAL_SECURITY = "financial_security"
    JOB_SECURITY = "job_security"
    HEALTH_SECURITY = "health_security"
    PERSONAL_SECURITY = "personal_security"
    ENVIRONMENTAL_SAFETY = "environmental_safety"
    
    # Digital Well-being (Level 3)
    DIGITAL_SECURITY = "digital_security"
    TECH_LITERACY = "tech_literacy"
    ONLINE_IDENTITY = "online_identity"
    INFORMATION_HYGIENE = "information_hygiene"
    AI_AUTOMATION = "ai_automation"
    SCREEN_TIME_BALANCE = "screen_time_balance"
    
    # Love & Belonging (Level 4)
    FAMILY_CONNECTION = "family_connection"
    FRIENDSHIP = "friendship"
    SOCIAL_CONTACT = "social_contact"
    GROUP_BELONGING = "group_belonging"
    ACCEPTANCE = "acceptance"
    COMMUNICATION = "communication"
    
    # Esteem (Level 5)
    SELF_WORTH = "self_worth"
    RECOGNITION = "recognition"
    COMPETENCE = "competence"
    INDEPENDENCE = "independence"
    RESPECT = "respect"
    ACHIEVEMENT = "achievement"
    
    # Self-Actualization (Level 6)
    MEANING = "meaning"
    GROWTH = "growth"
    PURPOSE = "purpose"
    POTENTIAL = "potential"
    AUTHENTICITY = "authenticity"
    VALUES = "values"

class NeedTier(Enum):
    """Individual need tiers (1-4) within each category"""
    TIER_1 = 1  # Basic survival/functioning
    TIER_2 = 2  # Stability and security
    TIER_3 = 3  # Comfort and routine
    TIER_4 = 4  # Thriving and flourishing

class NeedsAssessment:
    """Manages user needs assessment and tracking"""
    
    def __init__(self):
        self.needs_structure = self._initialize_needs_structure()
    
    def _initialize_needs_structure(self) -> Dict:
        """Initialize the complete needs structure with descriptions"""
        return {
            NeedLevel.ESSENTIAL: {
                NeedCategory.FOOD: {
                    "name": "Food",
                    "description": "Nutrition and sustenance",
                    "tiers": {
                        NeedTier.TIER_1: "I have enough food to eat today.",
                        NeedTier.TIER_2: "I have food that gives me energy for the whole day.",
                        NeedTier.TIER_3: "I enjoy most of my meals.",
                        NeedTier.TIER_4: "I can make food choices based on what my body needs."
                    }
                },
                NeedCategory.WATER: {
                    "name": "Water",
                    "description": "Hydration and clean water access",
                    "tiers": {
                        NeedTier.TIER_1: "I can get clean water when I need it.",
                        NeedTier.TIER_2: "I can drink clean water at home.",
                        NeedTier.TIER_3: "I drink water throughout the day without thinking about it.",
                        NeedTier.TIER_4: "Staying hydrated is just part of my normal day."
                    }
                },
                NeedCategory.REST: {
                    "name": "Rest",
                    "description": "Sleep and recovery",
                    "tiers": {
                        NeedTier.TIER_1: "I have a safe place to sleep.",
                        NeedTier.TIER_2: "I wake up feeling rested most days.",
                        NeedTier.TIER_3: "I have a bedtime routine.",
                        NeedTier.TIER_4: "Sleep gives me energy for everything I want to do."
                    }
                },
                NeedCategory.MOVEMENT: {
                    "name": "Movement",
                    "description": "Physical activity and mobility",
                    "tiers": {
                        NeedTier.TIER_1: "I can move around in a way that works for my body.",
                        NeedTier.TIER_2: "I can move without pain most days.",
                        NeedTier.TIER_3: "I look forward to moving my body.",
                        NeedTier.TIER_4: "Moving my body makes me feel strong."
                    }
                },
                NeedCategory.SHELTER: {
                    "name": "Shelter",
                    "description": "Safe and comfortable living space",
                    "tiers": {
                        NeedTier.TIER_1: "I have a place that keeps me dry and safe.",
                        NeedTier.TIER_2: "My home feels safe from dangers inside and outside.",
                        NeedTier.TIER_3: "My home is a place I want to be.",
                        NeedTier.TIER_4: "My home supports the life I want."
                    }
                },
                NeedCategory.HEALTH: {
                    "name": "Health",
                    "description": "Medical care and wellness",
                    "tiers": {
                        NeedTier.TIER_1: "I can get help when I don't feel well.",
                        NeedTier.TIER_2: "I can get medicine when I need it.",
                        NeedTier.TIER_3: "I know what keeps my body feeling good.",
                        NeedTier.TIER_4: "I am taking steps now for my future health."
                    }
                }
            }
            # Additional levels will be added in subsequent updates
        }
    
    def get_needs_for_level(self, level: NeedLevel) -> Dict:
        """Get all needs for a specific level"""
        return self.needs_structure.get(level, {})
    
    def get_need_description(self, category: NeedCategory, tier: NeedTier) -> str:
        """Get the description for a specific need and tier"""
        for level in self.needs_structure.values():
            if category in level:
                return level[category]["tiers"].get(tier, "Description not available")
        return "Need not found"
    
    def assess_user_needs(self, user_id: str, responses: Dict[NeedCategory, NeedTier]) -> Dict:
        """Assess user needs based on their responses"""
        assessment = {
            "user_id": user_id,
            "assessment_date": datetime.now().isoformat(),
            "overall_score": 0,
            "level_scores": {},
            "needs_status": {}
        }
        
        total_score = 0
        total_possible = 0
        
        for level, needs in self.needs_structure.items():
            level_score = 0
            level_possible = 0
            
            for category in needs.keys():
                if category in responses:
                    tier = responses[category]
                    score = tier.value
                    level_score += score
                    level_possible += 4  # Max tier is 4
                    
                    assessment["needs_status"][category.value] = {
                        "tier": tier.value,
                        "description": self.get_need_description(category, tier),
                        "level": level.value
                    }
            
            assessment["level_scores"][level.name] = {
                "score": level_score,
                "possible": level_possible,
                "percentage": (level_score / level_possible * 100) if level_possible > 0 else 0
            }
            
            total_score += level_score
            total_possible += level_possible
        
        assessment["overall_score"] = (total_score / total_possible * 100) if total_possible > 0 else 0
        
        return assessment
    
    def get_priority_needs(self, assessment: Dict) -> List[Dict]:
        """Get the highest priority needs that need attention"""
        priority_needs = []
        
        for category, status in assessment["needs_status"].items():
            if status["tier"] <= 2:  # Tiers 1-2 need attention
                priority_needs.append({
                    "category": category,
                    "tier": status["tier"],
                    "description": status["description"],
                    "level": status["level"],
                    "priority": "high" if status["tier"] == 1 else "medium"
                })
        
        # Sort by priority (tier 1 first, then by level)
        priority_needs.sort(key=lambda x: (x["tier"], x["level"]))
        return priority_needs
    
    def generate_needs_prompt(self, assessment: Dict, priority_needs: List[Dict]) -> str:
        """Generate a prompt for the AI to address user needs"""
        if not priority_needs:
            return "The user appears to have their basic needs well met. Continue to support their growth and well-being."
        
        prompt = "Based on the user's needs assessment, focus on supporting these priority areas:\n\n"
        
        for need in priority_needs:
            prompt += f"• {need['category'].replace('_', ' ').title()}: {need['description']}\n"
        
        prompt += "\nProvide supportive, understanding responses that acknowledge these challenges and offer practical suggestions when appropriate."
        return prompt

class MemoryEnhancer:
    """Enhances memory storage and retrieval using needs framework"""
    
    def __init__(self, needs_assessment: NeedsAssessment):
        self.needs_assessment = needs_assessment
    
    def enhance_memory_storage(self, user_id: str, content: str, memory_type: str, 
                              needs_context: Optional[Dict] = None) -> Dict:
        """Enhance memory storage with needs context"""
        enhanced_memory = {
            "user_id": user_id,
            "content": content,
            "memory_type": memory_type,
            "needs_context": needs_context or {},
            "timestamp": datetime.now().isoformat(),
            "priority_score": self._calculate_priority_score(content, needs_context)
        }
        
        return enhanced_memory
    
    def _calculate_priority_score(self, content: str, needs_context: Dict) -> float:
        """Calculate priority score for memory based on needs context"""
        base_score = 1.0
        
        # Increase priority if content relates to priority needs
        if needs_context and "priority_needs" in needs_context:
            for need in needs_context["priority_needs"]:
                if need["category"].lower() in content.lower():
                    base_score += 0.5
                    if need["priority"] == "high":
                        base_score += 0.3
        
        return min(base_score, 3.0)  # Cap at 3.0
    
    def generate_context_aware_response(self, user_id: str, query: str, 
                                      user_needs: Dict, conversation_history: List[Dict]) -> str:
        """Generate context-aware response considering user needs"""
        # This will be expanded to integrate with the AI response generation
        context_prompt = self.needs_assessment.generate_needs_prompt(user_needs, 
                                                                  user_needs.get("priority_needs", []))
        
        return f"Context: {context_prompt}\n\nUser Query: {query}"

# Initialize the framework
needs_framework = NeedsAssessment()
memory_enhancer = MemoryEnhancer(needs_framework)
