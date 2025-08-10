"""
Wellbeing Framework for RAG Companions

This module implements a modern hierarchy of needs framework that companions
use to understand and support user wellbeing across multiple dimensions.
"""

from typing import Dict, List, Optional, Tuple, Any
from dataclasses import dataclass, field
from enum import Enum
from datetime import datetime, timedelta
import json
import uuid

class WellbeingDimension(Enum):
    """Core wellbeing dimensions"""
    ESSENTIAL = "essential"
    SAFETY = "safety"
    BELONGING = "belonging"
    ESTEEM = "esteem"
    COGNITIVE = "cognitive"
    AESTHETIC = "aesthetic"
    TRANSCENDENCE = "transcendence"
    SELF_ACTUALIZATION = "self_actualization"

class WellbeingTier(Enum):
    """Wellbeing assessment tiers"""
    TIER_1 = 1  # Basic needs met
    TIER_2 = 2  # Comfortable
    TIER_3 = 3  # Satisfied
    TIER_4 = 4  # Thriving
    TIER_5 = 5  # Optimal

@dataclass
class WellbeingMetric:
    """Individual wellbeing metric with assessment"""
    name: str
    description: str
    current_tier: WellbeingTier = WellbeingTier.TIER_1
    assessment_date: datetime = field(default_factory=datetime.now)
    notes: str = ""
    improvement_suggestions: List[str] = field(default_factory=list)
    
    def assess_tier(self, assessment: str) -> WellbeingTier:
        """Assess current tier based on user input"""
        assessment_lower = assessment.lower()
        
        # Tier 1: Basic needs met
        if any(word in assessment_lower for word in ["enough", "basic", "minimal", "surviving"]):
            self.current_tier = WellbeingTier.TIER_1
        # Tier 2: Comfortable
        elif any(word in assessment_lower for word in ["comfortable", "adequate", "stable", "okay"]):
            self.current_tier = WellbeingTier.TIER_2
        # Tier 3: Satisfied
        elif any(word in assessment_lower for word in ["satisfied", "good", "content", "happy"]):
            self.current_tier = WellbeingTier.TIER_3
        # Tier 4: Thriving
        elif any(word in assessment_lower for word in ["thriving", "excellent", "amazing", "optimal"]):
            self.current_tier = WellbeingTier.TIER_4
        # Tier 5: Optimal
        elif any(word in assessment_lower for word in ["optimal", "peak", "best", "exceptional"]):
            self.current_tier = WellbeingTier.TIER_5
        else:
            # Default to current tier if unclear
            pass
        
        self.assessment_date = datetime.now()
        return self.current_tier
    
    def get_tier_description(self) -> str:
        """Get human-readable description of current tier"""
        tier_descriptions = {
            WellbeingTier.TIER_1: "Basic needs are being met",
            WellbeingTier.TIER_2: "Comfortable and stable",
            WellbeingTier.TIER_3: "Satisfied and content",
            WellbeingTier.TIER_4: "Thriving and flourishing",
            WellbeingTier.TIER_5: "Optimal and exceptional"
        }
        return tier_descriptions.get(self.current_tier, "Unknown tier")
    
    def to_dict(self) -> Dict:
        """Convert to dictionary for storage"""
        return {
            "name": self.name,
            "description": self.description,
            "current_tier": self.current_tier.value,
            "assessment_date": self.assessment_date.isoformat(),
            "notes": self.notes,
            "improvement_suggestions": self.improvement_suggestions
        }
    
    @classmethod
    def from_dict(cls, data: Dict) -> 'WellbeingMetric':
        """Create from dictionary"""
        return cls(
            name=data["name"],
            description=data["description"],
            current_tier=WellbeingTier(data["current_tier"]),
            assessment_date=datetime.fromisoformat(data["assessment_date"]),
            notes=data.get("notes", ""),
            improvement_suggestions=data.get("improvement_suggestions", [])
        )

class WellbeingFramework:
    """
    Modern hierarchy of needs framework for companion wellbeing support
    """
    
    def __init__(self):
        self.dimensions = self._initialize_dimensions()
        self.assessment_history = []
        self.improvement_tracking = {}
        self.goal_suggestions = {}
    
    def _initialize_dimensions(self) -> Dict[WellbeingDimension, Dict[str, Any]]:
        """Initialize the wellbeing dimensions with their metrics"""
        return {
            WellbeingDimension.ESSENTIAL: {
                "name": "Essential Needs",
                "description": "Basic physiological and survival needs",
                "metrics": {
                    "food": WellbeingMetric(
                        "Food",
                        "Nutrition and sustenance needs",
                        improvement_suggestions=[
                            "Ensure regular meal times",
                            "Include variety in diet",
                            "Stay hydrated throughout the day",
                            "Plan meals ahead when possible"
                        ]
                    ),
                    "water": WellbeingMetric(
                        "Water",
                        "Hydration needs",
                        improvement_suggestions=[
                            "Drink water regularly",
                            "Carry a water bottle",
                            "Set hydration reminders",
                            "Monitor urine color for hydration"
                        ]
                    ),
                    "sleep": WellbeingMetric(
                        "Sleep",
                        "Rest and recovery needs",
                        improvement_suggestions=[
                            "Maintain consistent sleep schedule",
                            "Create relaxing bedtime routine",
                            "Optimize sleep environment",
                            "Limit screen time before bed"
                        ]
                    ),
                    "physical_activity": WellbeingMetric(
                        "Physical Activity",
                        "Movement and exercise needs",
                        improvement_suggestions=[
                            "Start with small daily movements",
                            "Find activities you enjoy",
                            "Set realistic fitness goals",
                            "Include both cardio and strength training"
                        ]
                    )
                }
            },
            WellbeingDimension.SAFETY: {
                "name": "Safety & Security",
                "description": "Physical, emotional, and financial security",
                "metrics": {
                    "physical_safety": WellbeingMetric(
                        "Physical Safety",
                        "Freedom from physical harm",
                        improvement_suggestions=[
                            "Learn self-defense basics",
                            "Be aware of surroundings",
                            "Trust your instincts",
                            "Have emergency contacts ready"
                        ]
                    ),
                    "emotional_safety": WellbeingMetric(
                        "Emotional Safety",
                        "Freedom from emotional harm",
                        improvement_suggestions=[
                            "Set healthy boundaries",
                            "Practice self-compassion",
                            "Seek supportive relationships",
                            "Learn emotional regulation techniques"
                        ]
                    ),
                    "financial_security": WellbeingMetric(
                        "Financial Security",
                        "Basic financial stability",
                        improvement_suggestions=[
                            "Create emergency fund",
                            "Track income and expenses",
                            "Set financial goals",
                            "Learn basic money management"
                        ]
                    ),
                    "health_security": WellbeingMetric(
                        "Health Security",
                        "Access to healthcare and wellness",
                        improvement_suggestions=[
                            "Regular health checkups",
                            "Maintain health insurance",
                            "Learn about preventive care",
                            "Build relationship with healthcare providers"
                        ]
                    )
                }
            },
            WellbeingDimension.BELONGING: {
                "name": "Belonging & Connection",
                "description": "Social connections and community",
                "metrics": {
                    "family_connections": WellbeingMetric(
                        "Family Connections",
                        "Relationships with family members",
                        improvement_suggestions=[
                            "Schedule regular family time",
                            "Practice active listening",
                            "Express appreciation regularly",
                            "Resolve conflicts constructively"
                        ]
                    ),
                    "friendships": WellbeingMetric(
                        "Friendships",
                        "Close personal relationships",
                        improvement_suggestions=[
                            "Reach out to old friends",
                            "Join social groups or clubs",
                            "Be a good listener",
                            "Show genuine interest in others"
                        ]
                    ),
                    "romantic_relationships": WellbeingMetric(
                        "Romantic Relationships",
                        "Intimate partnerships",
                        improvement_suggestions=[
                            "Communicate openly and honestly",
                            "Show appreciation and affection",
                            "Respect boundaries and needs",
                            "Work on personal growth together"
                        ]
                    ),
                    "community_involvement": WellbeingMetric(
                        "Community Involvement",
                        "Connection to broader community",
                        improvement_suggestions=[
                            "Volunteer for causes you care about",
                            "Attend community events",
                            "Join local organizations",
                            "Support local businesses"
                        ]
                    )
                }
            },
            WellbeingDimension.ESTEEM: {
                "name": "Esteem & Recognition",
                "description": "Self-worth and achievement",
                "metrics": {
                    "self_confidence": WellbeingMetric(
                        "Self-Confidence",
                        "Belief in one's abilities",
                        improvement_suggestions=[
                            "Celebrate small wins",
                            "Practice positive self-talk",
                            "Step outside comfort zone",
                            "Learn from failures constructively"
                        ]
                    ),
                    "achievement": WellbeingMetric(
                        "Achievement",
                        "Sense of accomplishment",
                        improvement_suggestions=[
                            "Set SMART goals",
                            "Break large goals into smaller steps",
                            "Track progress regularly",
                            "Reward yourself for milestones"
                        ]
                    ),
                    "recognition": WellbeingMetric(
                        "Recognition",
                        "Acknowledgment from others",
                        improvement_suggestions=[
                            "Share your accomplishments",
                            "Ask for feedback",
                            "Recognize others' achievements",
                            "Build a portfolio of your work"
                        ]
                    ),
                    "independence": WellbeingMetric(
                        "Independence",
                        "Self-reliance and autonomy",
                        improvement_suggestions=[
                            "Learn new skills",
                            "Make decisions independently",
                            "Take responsibility for choices",
                            "Build problem-solving confidence"
                        ]
                    )
                }
            },
            WellbeingDimension.COGNITIVE: {
                "name": "Cognitive Growth",
                "description": "Learning, creativity, and mental stimulation",
                "metrics": {
                    "learning": WellbeingMetric(
                        "Learning",
                        "Continuous education and skill development",
                        improvement_suggestions=[
                            "Read regularly",
                            "Take online courses",
                            "Learn a new language",
                            "Explore new hobbies"
                        ]
                    ),
                    "creativity": WellbeingMetric(
                        "Creativity",
                        "Artistic expression and innovation",
                        improvement_suggestions=[
                            "Try different art forms",
                            "Brainstorm creative solutions",
                            "Keep a creativity journal",
                            "Collaborate with creative people"
                        ]
                    ),
                    "problem_solving": WellbeingMetric(
                        "Problem Solving",
                        "Analytical thinking and decision making",
                        improvement_suggestions=[
                            "Practice puzzles and games",
                            "Learn new problem-solving techniques",
                            "Analyze different perspectives",
                            "Break complex problems into parts"
                        ]
                    ),
                    "intellectual_stimulation": WellbeingMetric(
                        "Intellectual Stimulation",
                        "Mental challenges and growth",
                        improvement_suggestions=[
                            "Engage in debates and discussions",
                            "Attend lectures and workshops",
                            "Join intellectual communities",
                            "Challenge your assumptions"
                        ]
                    )
                }
            },
            WellbeingDimension.AESTHETIC: {
                "name": "Aesthetic & Beauty",
                "description": "Appreciation of beauty and art",
                "metrics": {
                    "art_appreciation": WellbeingMetric(
                        "Art Appreciation",
                        "Enjoyment of visual arts",
                        improvement_suggestions=[
                            "Visit art galleries and museums",
                            "Learn about different art styles",
                            "Create your own art",
                            "Follow artists you admire"
                        ]
                    ),
                    "nature_connection": WellbeingMetric(
                        "Nature Connection",
                        "Connection to natural world",
                        improvement_suggestions=[
                            "Spend time outdoors regularly",
                            "Practice mindfulness in nature",
                            "Learn about local ecosystems",
                            "Support environmental causes"
                        ]
                    ),
                    "beauty_awareness": WellbeingMetric(
                        "Beauty Awareness",
                        "Recognition of beauty in daily life",
                        improvement_suggestions=[
                            "Notice beauty in small things",
                            "Create beautiful spaces",
                            "Practice gratitude for beauty",
                            "Share beauty with others"
                        ]
                    ),
                    "sensory_enjoyment": WellbeingMetric(
                        "Sensory Enjoyment",
                        "Pleasure from sensory experiences",
                        improvement_suggestions=[
                            "Explore different textures",
                            "Listen to diverse music",
                            "Savor food mindfully",
                            "Create pleasant environments"
                        ]
                    )
                }
            },
            WellbeingDimension.TRANSCENDENCE: {
                "name": "Transcendence & Spirituality",
                "description": "Connection to something greater",
                "metrics": {
                    "spiritual_connection": WellbeingMetric(
                        "Spiritual Connection",
                        "Connection to spiritual beliefs",
                        improvement_suggestions=[
                            "Explore spiritual practices",
                            "Connect with spiritual community",
                            "Practice meditation or prayer",
                            "Read spiritual texts"
                        ]
                    ),
                    "purpose_meaning": WellbeingMetric(
                        "Purpose & Meaning",
                        "Sense of life purpose",
                        improvement_suggestions=[
                            "Reflect on your values",
                            "Identify what matters most",
                            "Set meaningful goals",
                            "Help others find purpose"
                        ]
                    ),
                    "gratitude": WellbeingMetric(
                        "Gratitude",
                        "Appreciation for life's blessings",
                        improvement_suggestions=[
                            "Keep a gratitude journal",
                            "Express thanks to others",
                            "Notice small blessings",
                            "Practice daily gratitude"
                        ]
                    ),
                    "forgiveness": WellbeingMetric(
                        "Forgiveness",
                        "Letting go of resentment",
                        improvement_suggestions=[
                            "Practice self-forgiveness",
                            "Let go of grudges",
                            "Seek reconciliation when possible",
                            "Learn from past experiences"
                        ]
                    )
                }
            },
            WellbeingDimension.SELF_ACTUALIZATION: {
                "name": "Self-Actualization",
                "description": "Reaching full potential",
                "metrics": {
                    "personal_growth": WellbeingMetric(
                        "Personal Growth",
                        "Continuous self-improvement",
                        improvement_suggestions=[
                            "Set growth-oriented goals",
                            "Embrace challenges as opportunities",
                            "Learn from all experiences",
                            "Invest in personal development"
                        ]
                    ),
                    "authenticity": WellbeingMetric(
                        "Authenticity",
                        "Living true to oneself",
                        improvement_suggestions=[
                            "Know your core values",
                            "Express your true self",
                            "Make choices aligned with values",
                            "Build genuine relationships"
                        ]
                    ),
                    "fulfillment": WellbeingMetric(
                        "Fulfillment",
                        "Deep satisfaction with life",
                        improvement_suggestions=[
                            "Pursue your passions",
                            "Contribute to causes you care about",
                            "Build meaningful relationships",
                            "Create lasting impact"
                        ]
                    ),
                    "wisdom": WellbeingMetric(
                        "Wisdom",
                        "Deep understanding and insight",
                        improvement_suggestions=[
                            "Reflect on life experiences",
                            "Learn from others' wisdom",
                            "Practice mindfulness",
                            "Share insights with others"
                        ]
                    )
                }
            }
        }
    
    def assess_wellbeing(self, dimension: WellbeingDimension, metric_name: str, 
                        assessment: str, notes: str = "") -> WellbeingMetric:
        """Assess a specific wellbeing metric"""
        if dimension not in self.dimensions:
            raise ValueError(f"Unknown dimension: {dimension}")
        
        metrics = self.dimensions[dimension]["metrics"]
        if metric_name not in metrics:
            raise ValueError(f"Unknown metric: {metric_name} in dimension {dimension}")
        
        metric = metrics[metric_name]
        metric.assess_tier(assessment)
        metric.notes = notes
        
        # Record assessment
        assessment_record = {
            "id": str(uuid.uuid4()),
            "timestamp": datetime.now().isoformat(),
            "dimension": dimension.value,
            "metric": metric_name,
            "assessment": assessment,
            "tier": metric.current_tier.value,
            "notes": notes
        }
        self.assessment_history.append(assessment_record)
        
        return metric
    
    def get_wellbeing_summary(self) -> Dict:
        """Get comprehensive wellbeing summary"""
        summary = {}
        
        for dimension, dim_data in self.dimensions.items():
            summary[dimension.value] = {
                "name": dim_data["name"],
                "description": dim_data["description"],
                "metrics": {}
            }
            
            for metric_name, metric in dim_data["metrics"].items():
                summary[dimension.value]["metrics"][metric_name] = {
                    "current_tier": metric.current_tier.value,
                    "tier_description": metric.get_tier_description(),
                    "last_assessment": metric.assessment_date.isoformat(),
                    "notes": metric.notes,
                    "improvement_suggestions": metric.improvement_suggestions
                }
        
        return summary
    
    def get_priority_areas(self, count: int = 3) -> List[Dict]:
        """Get areas that need attention, prioritized by current tier"""
        priority_areas = []
        
        for dimension, dim_data in self.dimensions.items():
            for metric_name, metric in dim_data["metrics"].items():
                priority_areas.append({
                    "dimension": dimension.value,
                    "dimension_name": dim_data["name"],
                    "metric": metric_name,
                    "metric_name": metric.name,
                    "current_tier": metric.current_tier.value,
                    "description": metric.description,
                    "improvement_suggestions": metric.improvement_suggestions
                })
        
        # Sort by tier (lower tiers need more attention)
        priority_areas.sort(key=lambda x: x["current_tier"])
        return priority_areas[:count]
    
    def get_improvement_suggestions(self, dimension: WellbeingDimension = None) -> Dict:
        """Get improvement suggestions for wellbeing areas"""
        suggestions = {}
        
        if dimension:
            # Specific dimension
            if dimension not in self.dimensions:
                raise ValueError(f"Unknown dimension: {dimension}")
            
            dim_data = self.dimensions[dimension]
            suggestions[dimension.value] = {
                "name": dim_data["name"],
                "suggestions": {}
            }
            
            for metric_name, metric in dim_data["metrics"].items():
                if metric.current_tier.value <= 2:  # Focus on lower tiers
                    suggestions[dimension.value]["suggestions"][metric_name] = {
                        "current_tier": metric.current_tier.value,
                        "suggestions": metric.improvement_suggestions
                    }
        else:
            # All dimensions
            for dim, dim_data in self.dimensions.items():
                suggestions[dim.value] = {
                    "name": dim_data["name"],
                    "suggestions": {}
                }
                
                for metric_name, metric in dim_data["metrics"].items():
                    if metric.current_tier.value <= 2:  # Focus on lower tiers
                        suggestions[dim.value]["suggestions"][metric_name] = {
                            "current_tier": metric.current_tier.value,
                            "suggestions": metric.improvement_suggestions
                        }
        
        return suggestions
    
    def track_improvement(self, dimension: WellbeingDimension, metric_name: str, 
                         action_taken: str, impact: str) -> None:
        """Track improvement actions and their impact"""
        tracking_key = f"{dimension.value}_{metric_name}"
        
        if tracking_key not in self.improvement_tracking:
            self.improvement_tracking[tracking_key] = []
        
        tracking_record = {
            "timestamp": datetime.now().isoformat(),
            "action": action_taken,
            "impact": impact,
            "dimension": dimension.value,
            "metric": metric_name
        }
        
        self.improvement_tracking[tracking_key].append(tracking_record)
    
    def get_improvement_history(self, dimension: WellbeingDimension = None, 
                               metric_name: str = None) -> Dict:
        """Get history of improvement actions"""
        if dimension and metric_name:
            # Specific metric
            tracking_key = f"{dimension.value}_{metric_name}"
            return {
                "tracking_key": tracking_key,
                "history": self.improvement_tracking.get(tracking_key, [])
            }
        elif dimension:
            # All metrics in dimension
            history = {}
            for metric_name in self.dimensions[dimension]["metrics"].keys():
                tracking_key = f"{dimension.value}_{metric_name}"
                history[metric_name] = self.improvement_tracking.get(tracking_key, [])
            return history
        else:
            # All tracking
            return self.improvement_tracking
    
    def to_dict(self) -> Dict:
        """Convert framework to dictionary for storage"""
        return {
            "dimensions": {
                dim.value: {
                    "name": data["name"],
                    "description": data["description"],
                    "metrics": {
                        name: metric.to_dict() 
                        for name, metric in data["metrics"].items()
                    }
                }
                for dim, data in self.dimensions.items()
            },
            "assessment_history": self.assessment_history,
            "improvement_tracking": self.improvement_tracking
        }
    
    @classmethod
    def from_dict(cls, data: Dict) -> 'WellbeingFramework':
        """Create framework from dictionary"""
        framework = cls()
        
        # Restore dimensions and metrics
        for dim_value, dim_data in data["dimensions"].items():
            dimension = WellbeingDimension(dim_value)
            framework.dimensions[dimension]["name"] = dim_data["name"]
            framework.dimensions[dimension]["description"] = dim_data["description"]
            
            for metric_name, metric_data in dim_data["metrics"].items():
                framework.dimensions[dimension]["metrics"][metric_name] = \
                    WellbeingMetric.from_dict(metric_data)
        
        # Restore history and tracking
        framework.assessment_history = data.get("assessment_history", [])
        framework.improvement_tracking = data.get("improvement_tracking", {})
        
        return framework
