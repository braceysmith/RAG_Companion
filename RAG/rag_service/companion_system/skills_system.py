"""
Skills System for RAG Companions

This module implements an expandable skills system that allows companions
to be aware of and express their capabilities beyond just LLM functionality.
"""

import json
import logging
import uuid
from datetime import datetime, timedelta
from enum import Enum
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple, Union
from dataclasses import dataclass, asdict

logger = logging.getLogger(__name__)

class SkillCategory(Enum):
    """Categories for organizing skills"""
    COMMUNICATION = "communication"
    INFORMATION = "information"
    PRODUCTIVITY = "productivity"
    WELLBEING = "wellbeing"
    CALENDAR = "calendar"
    GOALS = "goals"
    REMINDERS = "reminders"
    UTILITY = "utility"
    CREATIVITY = "creativity"
    LEARNING = "learning"

class SkillPriority(Enum):
    """Priority levels for skill execution"""
    LOW = 1
    NORMAL = 2
    HIGH = 3
    CRITICAL = 4

class SkillStatus(Enum):
    """Status of skill execution"""
    PENDING = "pending"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"

@dataclass
class SkillParameter:
    """Parameter definition for a skill"""
    name: str
    description: str
    required: bool = True
    default_value: Any = None
    parameter_type: str = "string"  # string, number, boolean, list, object
    validation_rules: Optional[Dict[str, Any]] = None

@dataclass
class SkillResult:
    """Result of skill execution"""
    success: bool
    data: Optional[Dict[str, Any]] = None
    message: str = ""
    execution_time: float = 0.0
    metadata: Optional[Dict[str, Any]] = None
    error: Optional[str] = None

@dataclass
class SkillExecution:
    """Record of skill execution"""
    execution_id: str
    skill_name: str
    user_id: str
    parameters: Dict[str, Any]
    start_time: datetime
    end_time: Optional[datetime] = None
    status: SkillStatus = SkillStatus.PENDING
    result: Optional[SkillResult] = None
    priority: SkillPriority = SkillPriority.NORMAL
    context: Optional[Dict[str, Any]] = None

class BaseSkill:
    """Base class for all skills"""
    
    def __init__(self, name: str, description: str, category: SkillCategory):
        self.name = name
        self.description = description
        self.category = category
        self.parameters: List[SkillParameter] = []
        self.execution_history: List[SkillExecution] = []
        self.success_rate: float = 0.0
        self.avg_execution_time: float = 0.0
        self.total_executions: int = 0
        
    def validate_parameters(self, parameters: Dict[str, Any]) -> Tuple[bool, List[str]]:
        """Validate input parameters"""
        errors = []
        for param in self.parameters:
            if param.required and param.name not in parameters:
                errors.append(f"Required parameter '{param.name}' is missing")
            elif param.name in parameters:
                value = parameters[param.name]
                if not self._validate_parameter_value(param, value):
                    errors.append(f"Parameter '{param.name}' has invalid value: {value}")
        
        return len(errors) == 0, errors
    
    def _validate_parameter_value(self, param: SkillParameter, value: Any) -> bool:
        """Validate individual parameter value"""
        if param.validation_rules:
            # Add validation logic based on rules
            if "min_length" in param.validation_rules and len(str(value)) < param.validation_rules["min_length"]:
                return False
            if "max_length" in param.validation_rules and len(str(value)) > param.validation_rules["max_length"]:
                return False
            if "min_value" in param.validation_rules and value < param.validation_rules["min_value"]:
                return False
            if "max_value" in param.validation_rules and value > param.validation_rules["max_value"]:
                return False
        return True
    
    def execute(self, user_id: str, parameters: Dict[str, Any], 
                context: Optional[Dict[str, Any]] = None) -> SkillResult:
        """Execute the skill - to be implemented by subclasses"""
        raise NotImplementedError
    
    def get_capability_description(self) -> str:
        """Get human-readable description of what this skill can do"""
        return f"{self.name}: {self.description}"
    
    def get_usage_examples(self) -> List[str]:
        """Get example usage scenarios for this skill"""
        return []
    
    def can_execute(self, user_context: Dict[str, Any]) -> bool:
        """Check if skill can be executed given user context"""
        return True
    
    def estimate_execution_time(self, parameters: Dict[str, Any]) -> float:
        """Estimate execution time in seconds"""
        return 1.0  # Default 1 second
    
    def get_skill_chain_suggestions(self) -> List[List[str]]:
        """Get suggested skill chains that include this skill"""
        return []

class CalendarManagementSkill(BaseSkill):
    """Skill for managing calendar events and scheduling"""
    
    def __init__(self):
        super().__init__(
            name="calendar_management",
            description="Manage calendar events, scheduling, and time management",
            category=SkillCategory.CALENDAR
        )
        self.parameters = [
            SkillParameter("action", "Action to perform (create, read, update, delete, list)", required=True),
            SkillParameter("event_title", "Title of the calendar event", required=False),
            SkillParameter("start_time", "Start time of the event", required=False),
            SkillParameter("end_time", "End time of the event", required=False),
            SkillParameter("description", "Description of the event", required=False),
            SkillParameter("location", "Location of the event", required=False),
            SkillParameter("event_id", "ID of existing event for updates/deletes", required=False)
        ]
    
    def execute(self, user_id: str, parameters: Dict[str, Any], 
                context: Optional[Dict[str, Any]] = None) -> SkillResult:
        """Execute calendar management operations"""
        start_time = datetime.now()
        try:
            action = parameters.get("action", "").lower()
            
            if action == "create":
                result = self._create_event(user_id, parameters)
            elif action == "read":
                result = self._read_event(user_id, parameters)
            elif action == "update":
                result = self._update_event(user_id, parameters)
            elif action == "delete":
                result = self._delete_event(user_id, parameters)
            elif action == "list":
                result = self._list_events(user_id, parameters)
            else:
                return SkillResult(
                    success=False,
                    message=f"Unknown action: {action}",
                    error="Invalid action parameter"
                )
            
            execution_time = (datetime.now() - start_time).total_seconds()
            result.execution_time = execution_time
            return result
            
        except Exception as e:
            execution_time = (datetime.now() - start_time).total_seconds()
            return SkillResult(
                success=False,
                message=f"Calendar operation failed: {str(e)}",
                execution_time=execution_time,
                error=str(e)
            )
    
    def _create_event(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """Create a new calendar event"""
        # This would integrate with actual calendar system
        event_data = {
            "event_id": str(uuid.uuid4()),
            "user_id": user_id,
            "title": parameters.get("event_title", "Untitled Event"),
            "start_time": parameters.get("start_time"),
            "end_time": parameters.get("end_time"),
            "description": parameters.get("description", ""),
            "location": parameters.get("location", "")
        }
        
        return SkillResult(
            success=True,
            data=event_data,
            message=f"Created calendar event: {event_data['title']}"
        )
    
    def _read_event(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """Read calendar event details"""
        event_id = parameters.get("event_id")
        if not event_id:
            return SkillResult(
                success=False,
                message="Event ID is required for reading events",
                error="Missing event_id parameter"
            )
        
        # This would fetch from actual calendar system
        event_data = {
            "event_id": event_id,
            "title": "Sample Event",
            "start_time": "2024-01-01T10:00:00",
            "description": "This is a sample event"
        }
        
        return SkillResult(
            success=True,
            data=event_data,
            message=f"Retrieved event: {event_data['title']}"
        )
    
    def _update_event(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """Update existing calendar event"""
        event_id = parameters.get("event_id")
        if not event_id:
            return SkillResult(
                success=False,
                message="Event ID is required for updating events",
                error="Missing event_id parameter"
            )
        
        return SkillResult(
            success=True,
            data={"event_id": event_id},
            message=f"Updated event: {event_id}"
        )
    
    def _delete_event(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """Delete calendar event"""
        event_id = parameters.get("event_id")
        if not event_id:
            return SkillResult(
                success=False,
                message="Event ID is required for deleting events",
                error="Missing event_id parameter"
            )
        
        return SkillResult(
            success=True,
            data={"event_id": event_id},
            message=f"Deleted event: {event_id}"
        )
    
    def _list_events(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """List calendar events"""
        # This would fetch from actual calendar system
        events = [
            {"event_id": "1", "title": "Meeting", "start_time": "2024-01-01T10:00:00"},
            {"event_id": "2", "title": "Lunch", "start_time": "2024-01-01T12:00:00"}
        ]
        
        return SkillResult(
            success=True,
            data={"events": events, "count": len(events)},
            message=f"Found {len(events)} events"
        )

class GoalTrackingSkill(BaseSkill):
    """Skill for tracking and managing user goals"""
    
    def __init__(self):
        super().__init__(
            name="goal_tracking",
            description="Track progress on user goals and provide motivation",
            category=SkillCategory.GOALS
        )
        self.parameters = [
            SkillParameter("action", "Action to perform (create, update, check, list, complete)", required=True),
            SkillParameter("goal_title", "Title of the goal", required=False),
            SkillParameter("description", "Description of the goal", required=False),
            SkillParameter("target_date", "Target completion date", required=False),
            SkillParameter("goal_id", "ID of existing goal", required=False),
            SkillParameter("progress", "Progress percentage (0-100)", required=False),
            SkillParameter("notes", "Additional notes about the goal", required=False)
        ]
    
    def execute(self, user_id: str, parameters: Dict[str, Any], 
                context: Optional[Dict[str, Any]] = None) -> SkillResult:
        """Execute goal tracking operations"""
        start_time = datetime.now()
        try:
            action = parameters.get("action", "").lower()
            
            if action == "create":
                result = self._create_goal(user_id, parameters)
            elif action == "update":
                result = self._update_goal(user_id, parameters)
            elif action == "check":
                result = self._check_goal_progress(user_id, parameters)
            elif action == "list":
                result = self._list_goals(user_id, parameters)
            elif action == "complete":
                result = self._complete_goal(user_id, parameters)
            else:
                return SkillResult(
                    success=False,
                    message=f"Unknown action: {action}",
                    error="Invalid action parameter"
                )
            
            execution_time = (datetime.now() - start_time).total_seconds()
            result.execution_time = execution_time
            return result
            
        except Exception as e:
            execution_time = (datetime.now() - start_time).total_seconds()
            return SkillResult(
                success=False,
                message=f"Goal operation failed: {str(e)}",
                execution_time=execution_time,
                error=str(e)
            )
    
    def _create_goal(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """Create a new goal"""
        goal_data = {
            "goal_id": str(uuid.uuid4()),
            "user_id": user_id,
            "title": parameters.get("goal_title", "Untitled Goal"),
            "description": parameters.get("description", ""),
            "target_date": parameters.get("target_date"),
            "progress": 0,
            "created_date": datetime.now().isoformat(),
            "status": "active"
        }
        
        return SkillResult(
            success=True,
            data=goal_data,
            message=f"Created new goal: {goal_data['title']}"
        )
    
    def _update_goal(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """Update existing goal"""
        goal_id = parameters.get("goal_id")
        if not goal_id:
            return SkillResult(
                success=False,
                message="Goal ID is required for updating goals",
                error="Missing goal_id parameter"
            )
        
        return SkillResult(
            success=True,
            data={"goal_id": goal_id},
            message=f"Updated goal: {goal_id}"
        )
    
    def _check_goal_progress(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """Check progress on a specific goal"""
        goal_id = parameters.get("goal_id")
        if not goal_id:
            return SkillResult(
                success=False,
                message="Goal ID is required for checking progress",
                error="Missing goal_id parameter"
            )
        
        # This would fetch from actual goal system
        goal_data = {
            "goal_id": goal_id,
            "title": "Sample Goal",
            "progress": 75,
            "target_date": "2024-12-31"
        }
        
        return SkillResult(
            success=True,
            data=goal_data,
            message=f"Goal '{goal_data['title']}' is {goal_data['progress']}% complete"
        )
    
    def _list_goals(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """List user goals"""
        # This would fetch from actual goal system
        goals = [
            {"goal_id": "1", "title": "Learn Python", "progress": 60},
            {"goal_id": "2", "title": "Exercise Daily", "progress": 80}
        ]
        
        return SkillResult(
            success=True,
            data={"goals": goals, "count": len(goals)},
            message=f"Found {len(goals)} active goals"
        )
    
    def _complete_goal(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """Mark a goal as complete"""
        goal_id = parameters.get("goal_id")
        if not goal_id:
            return SkillResult(
                success=False,
                message="Goal ID is required for completing goals",
                error="Missing goal_id parameter"
            )
        
        return SkillResult(
            success=True,
            data={"goal_id": goal_id},
            message=f"Congratulations! Goal {goal_id} is now complete!"
        )

class ReminderSkill(BaseSkill):
    """Skill for managing reminders and notifications"""
    
    def __init__(self):
        super().__init__(
            name="reminder_management",
            description="Create, manage, and track reminders and notifications",
            category=SkillCategory.REMINDERS
        )
        self.parameters = [
            SkillParameter("action", "Action to perform (create, list, complete, delete)", required=True),
            SkillParameter("reminder_text", "Text of the reminder", required=False),
            SkillParameter("due_date", "When the reminder is due", required=False),
            SkillParameter("priority", "Priority level (low, normal, high, critical)", required=False),
            SkillParameter("reminder_id", "ID of existing reminder", required=False),
            SkillParameter("category", "Category of the reminder", required=False),
            SkillParameter("recurring", "Whether reminder repeats", required=False)
        ]
    
    def execute(self, user_id: str, parameters: Dict[str, Any], 
                context: Optional[Dict[str, Any]] = None) -> SkillResult:
        """Execute reminder management operations"""
        start_time = datetime.now()
        try:
            action = parameters.get("action", "").lower()
            
            if action == "create":
                result = self._create_reminder(user_id, parameters)
            elif action == "list":
                result = self._list_reminders(user_id, parameters)
            elif action == "complete":
                result = self._complete_reminder(user_id, parameters)
            elif action == "delete":
                result = self._delete_reminder(user_id, parameters)
            else:
                return SkillResult(
                    success=False,
                    message=f"Unknown action: {action}",
                    error="Invalid action parameter"
                )
            
            execution_time = (datetime.now() - start_time).total_seconds()
            result.execution_time = execution_time
            return result
            
        except Exception as e:
            execution_time = (datetime.now() - start_time).total_seconds()
            return SkillResult(
                success=False,
                message=f"Reminder operation failed: {str(e)}",
                execution_time=execution_time,
                error=str(e)
            )
    
    def _create_reminder(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """Create a new reminder"""
        reminder_data = {
            "reminder_id": str(uuid.uuid4()),
            "user_id": user_id,
            "text": parameters.get("reminder_text", "Untitled Reminder"),
            "due_date": parameters.get("due_date"),
            "priority": parameters.get("priority", "normal"),
            "category": parameters.get("category", "general"),
            "recurring": parameters.get("recurring", False),
            "created_date": datetime.now().isoformat(),
            "status": "pending"
        }
        
        return SkillResult(
            success=True,
            data=reminder_data,
            message=f"Created reminder: {reminder_data['text']}"
        )
    
    def _list_reminders(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """List user reminders"""
        # This would fetch from actual reminder system
        reminders = [
            {"reminder_id": "1", "text": "Call dentist", "due_date": "2024-01-15", "priority": "high"},
            {"reminder_id": "2", "text": "Buy groceries", "due_date": "2024-01-10", "priority": "normal"}
        ]
        
        return SkillResult(
            success=True,
            data={"reminders": reminders, "count": len(reminders)},
            message=f"Found {len(reminders)} active reminders"
        )
    
    def _complete_reminder(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """Mark a reminder as complete"""
        reminder_id = parameters.get("reminder_id")
        if not reminder_id:
            return SkillResult(
                success=False,
                message="Reminder ID is required for completing reminders",
                error="Missing reminder_id parameter"
            )
        
        return SkillResult(
            success=True,
            data={"reminder_id": reminder_id},
            message=f"Marked reminder {reminder_id} as complete"
        )
    
    def _delete_reminder(self, user_id: str, parameters: Dict[str, Any]) -> SkillResult:
        """Delete a reminder"""
        reminder_id = parameters.get("reminder_id")
        if not reminder_id:
            return SkillResult(
                success=False,
                message="Reminder ID is required for deleting reminders",
                error="Missing reminder_id parameter"
            )
        
        return SkillResult(
            success=True,
            data={"reminder_id": reminder_id},
            message=f"Deleted reminder: {reminder_id}"
        )

class UtilitySkill(BaseSkill):
    """Skill for utility operations like time, weather, calculations"""
    
    def __init__(self):
        super().__init__(
            name="utility_operations",
            description="Provide utility functions like time, weather, calculations",
            category=SkillCategory.UTILITY
        )
        self.parameters = [
            SkillParameter("operation", "Type of operation (time, weather, calculate, convert)", required=True),
            SkillParameter("location", "Location for weather or timezone", required=False),
            SkillParameter("expression", "Mathematical expression for calculations", required=False),
            SkillParameter("from_unit", "Source unit for conversions", required=False),
            SkillParameter("to_unit", "Target unit for conversions", required=False),
            SkillParameter("value", "Value to convert", required=False)
        ]
    
    def execute(self, user_id: str, parameters: Dict[str, Any], 
                context: Optional[Dict[str, Any]] = None) -> SkillResult:
        """Execute utility operations"""
        start_time = datetime.now()
        try:
            operation = parameters.get("operation", "").lower()
            
            if operation == "time":
                result = self._get_time(parameters)
            elif operation == "weather":
                result = self._get_weather(parameters)
            elif operation == "calculate":
                result = self._calculate(parameters)
            elif operation == "convert":
                result = self._convert_units(parameters)
            else:
                return SkillResult(
                    success=False,
                    message=f"Unknown operation: {operation}",
                    error="Invalid operation parameter"
                )
            
            execution_time = (datetime.now() - start_time).total_seconds()
            result.execution_time = execution_time
            return result
            
        except Exception as e:
            execution_time = (datetime.now() - start_time).total_seconds()
            return SkillResult(
                success=False,
                message=f"Utility operation failed: {str(e)}",
                execution_time=execution_time,
                error=str(e)
            )
    
    def _get_time(self, parameters: Dict[str, Any]) -> SkillResult:
        """Get current time information"""
        now = datetime.now()
        location = parameters.get("location", "local")
        
        time_data = {
            "current_time": now.strftime("%H:%M:%S"),
            "current_date": now.strftime("%Y-%m-%d"),
            "timezone": "local",
            "location": location
        }
        
        return SkillResult(
            success=True,
            data=time_data,
            message=f"Current time: {time_data['current_time']} on {time_data['current_date']}"
        )
    
    def _get_weather(self, parameters: Dict[str, Any]) -> SkillResult:
        """Get weather information"""
        location = parameters.get("location", "unknown")
        
        # This would integrate with actual weather API
        weather_data = {
            "location": location,
            "temperature": "22°C",
            "condition": "Partly cloudy",
            "humidity": "65%",
            "wind": "10 km/h"
        }
        
        return SkillResult(
            success=True,
            data=weather_data,
            message=f"Weather in {location}: {weather_data['temperature']}, {weather_data['condition']}"
        )
    
    def _calculate(self, parameters: Dict[str, Any]) -> SkillResult:
        """Perform mathematical calculations"""
        expression = parameters.get("expression", "")
        if not expression:
            return SkillResult(
                success=False,
                message="Expression is required for calculations",
                error="Missing expression parameter"
            )
        
        try:
            # Basic safety check - only allow safe mathematical operations
            allowed_chars = set("0123456789+-*/(). ")
            if not all(c in allowed_chars for c in expression):
                return SkillResult(
                    success=False,
                    message="Expression contains invalid characters",
                    error="Unsafe expression"
                )
            
            result = eval(expression)
            
            return SkillResult(
                success=True,
                data={"expression": expression, "result": result},
                message=f"{expression} = {result}"
            )
        except Exception as e:
            return SkillResult(
                success=False,
                message=f"Calculation failed: {str(e)}",
                error=str(e)
            )
    
    def _convert_units(self, parameters: Dict[str, Any]) -> SkillResult:
        """Convert between units"""
        from_unit = parameters.get("from_unit")
        to_unit = parameters.get("to_unit")
        value = parameters.get("value")
        
        if not all([from_unit, to_unit, value]):
            return SkillResult(
                success=False,
                message="from_unit, to_unit, and value are required for conversions",
                error="Missing required parameters"
            )
        
        try:
            value = float(value)
            # This would implement actual unit conversion logic
            converted_value = value * 1.0  # Placeholder
            
            return SkillResult(
                success=True,
                data={
                    "from_unit": from_unit,
                    "to_unit": to_unit,
                    "original_value": value,
                    "converted_value": converted_value
                },
                message=f"{value} {from_unit} = {converted_value} {to_unit}"
            )
        except ValueError:
            return SkillResult(
                success=False,
                message="Value must be a valid number",
                error="Invalid value parameter"
            )

class ReasoningSkill(BaseSkill):
    """Advanced reasoning skill that uses LLM for complex problem solving"""
    
    def __init__(self):
        super().__init__(
            name="Advanced Reasoning",
            description="Uses advanced reasoning to solve complex problems, analyze situations, and provide insights",
            category=SkillCategory.INFORMATION
        )
        
        # Define parameters for different reasoning tasks
        self.parameters = [
            SkillParameter(
                name="reasoning_type",
                description="Type of reasoning to perform (analysis, problem_solving, decision_making, creative_thinking)",
                required=True,
                parameter_type="string"
            ),
            SkillParameter(
                name="context",
                description="Context or background information for the reasoning task",
                required=False,
                parameter_type="string"
            ),
            SkillParameter(
                name="constraints",
                description="Constraints or limitations to consider",
                required=False,
                parameter_type="string"
            ),
            SkillParameter(
                name="output_format",
                description="Desired output format (structured, narrative, bullet_points)",
                required=False,
                parameter_type="string",
                default_value="structured"
            )
        ]
    
    def execute(self, user_id: str, parameters: Dict[str, Any], 
                context: Optional[Dict[str, Any]] = None) -> SkillResult:
        """Execute advanced reasoning task"""
        start_time = datetime.now()
        
        try:
            reasoning_type = parameters.get("reasoning_type", "analysis")
            context_info = parameters.get("context", "")
            constraints = parameters.get("constraints", "")
            output_format = parameters.get("output_format", "structured")
            
            # Perform reasoning based on type
            if reasoning_type == "analysis":
                result = self._perform_analysis(context_info, constraints, output_format)
            elif reasoning_type == "problem_solving":
                result = self._solve_problem(context_info, constraints, output_format)
            elif reasoning_type == "decision_making":
                result = self._make_decision(context_info, constraints, output_format)
            elif reasoning_type == "creative_thinking":
                result = self._creative_thinking(context_info, constraints, output_format)
            else:
                return SkillResult(
                    success=False,
                    error=f"Unknown reasoning type: {reasoning_type}",
                    execution_time=(datetime.now() - start_time).total_seconds()
                )
            
            execution_time = (datetime.now() - start_time).total_seconds()
            
            return SkillResult(
                success=True,
                data=result,
                message=f"Successfully completed {reasoning_type} reasoning task",
                execution_time=execution_time,
                metadata={
                    "reasoning_type": reasoning_type,
                    "output_format": output_format,
                    "context_length": len(context_info),
                    "constraints_count": len(constraints.split()) if constraints else 0
                }
            )
            
        except Exception as e:
            execution_time = (datetime.now() - start_time).total_seconds()
            return SkillResult(
                success=False,
                error=str(e),
                execution_time=execution_time
            )
    
    def _perform_analysis(self, context: str, constraints: str, output_format: str) -> Dict[str, Any]:
        """Perform detailed analysis of given context"""
        # This would integrate with an LLM for actual reasoning
        # For now, we'll provide a structured analysis framework
        
        analysis = {
            "key_insights": [],
            "patterns_identified": [],
            "potential_implications": [],
            "recommendations": [],
            "confidence_level": "medium"
        }
        
        if context:
            # Analyze context for key elements
            words = context.lower().split()
            if "problem" in words or "issue" in words:
                analysis["key_insights"].append("Problem-focused context detected")
            if "goal" in words or "objective" in words:
                analysis["key_insights"].append("Goal-oriented context identified")
            if "challenge" in words or "difficulty" in words:
                analysis["key_insights"].append("Challenging situation recognized")
        
        if constraints:
            analysis["constraints_analyzed"] = constraints.split(", ")
        
        if output_format == "bullet_points":
            analysis["formatted_output"] = self._format_as_bullets(analysis)
        elif output_format == "narrative":
            analysis["formatted_output"] = self._format_as_narrative(analysis)
        else:
            analysis["formatted_output"] = analysis
        
        return analysis
    
    def _solve_problem(self, context: str, constraints: str, output_format: str) -> Dict[str, Any]:
        """Solve problems using systematic approach"""
        solution = {
            "problem_statement": context,
            "solution_approach": "systematic_analysis",
            "steps": [
                "Define the problem clearly",
                "Identify root causes",
                "Generate potential solutions",
                "Evaluate alternatives",
                "Implement best solution",
                "Monitor and adjust"
            ],
            "constraints_considered": constraints.split(", ") if constraints else [],
            "estimated_complexity": "medium",
            "time_estimate": "2-4 hours"
        }
        
        if output_format == "bullet_points":
            solution["formatted_output"] = self._format_as_bullets(solution)
        elif output_format == "narrative":
            solution["formatted_output"] = self._format_as_narrative(solution)
        else:
            solution["formatted_output"] = solution
        
        return solution
    
    def _make_decision(self, context: str, constraints: str, output_format: str) -> Dict[str, Any]:
        """Make decisions using decision-making framework"""
        decision = {
            "decision_context": context,
            "decision_framework": "pros_cons_analysis",
            "options": ["Option A", "Option B", "Option C"],
            "evaluation_criteria": ["Feasibility", "Impact", "Cost", "Timeline"],
            "recommended_option": "Option B",
            "reasoning": "Balanced approach considering all constraints",
            "constraints": constraints.split(", ") if constraints else [],
            "confidence": "high"
        }
        
        if output_format == "bullet_points":
            decision["formatted_output"] = self._format_as_bullets(decision)
        elif output_format == "narrative":
            decision["formatted_output"] = self._format_as_narrative(decision)
        else:
            decision["formatted_output"] = decision
        
        return decision
    
    def _creative_thinking(self, context: str, constraints: str, output_format: str) -> Dict[str, Any]:
        """Generate creative ideas and solutions"""
        creativity = {
            "creative_context": context,
            "ideation_techniques": ["Brainstorming", "Mind Mapping", "Lateral Thinking"],
            "generated_ideas": [
                "Innovative approach A",
                "Creative solution B",
                "Out-of-the-box idea C"
            ],
            "inspiration_sources": ["Nature", "Other industries", "Historical examples"],
            "constraints_as_opportunities": constraints.split(", ") if constraints else [],
            "novelty_score": "high"
        }
        
        if output_format == "bullet_points":
            creativity["formatted_output"] = self._format_as_bullets(creativity)
        elif output_format == "narrative":
            creativity["formatted_output"] = self._format_as_narrative(creativity)
        else:
            creativity["formatted_output"] = creativity
        
        return creativity
    
    def _format_as_bullets(self, data: Dict[str, Any]) -> str:
        """Format data as bullet points"""
        formatted = []
        for key, value in data.items():
            if isinstance(value, list):
                formatted.append(f"• {key.replace('_', ' ').title()}:")
                for item in value:
                    formatted.append(f"  - {item}")
            else:
                formatted.append(f"• {key.replace('_', ' ').title()}: {value}")
        return "\n".join(formatted)
    
    def _format_as_narrative(self, data: Dict[str, Any]) -> str:
        """Format data as narrative text"""
        narrative = []
        for key, value in data.items():
            if isinstance(value, list):
                narrative.append(f"The {key.replace('_', ' ')} includes: {', '.join(value)}.")
            else:
                narrative.append(f"The {key.replace('_', ' ')} is: {value}.")
        return " ".join(narrative)
    
    def get_capability_description(self) -> str:
        return "Advanced reasoning capabilities for complex problem solving, analysis, decision making, and creative thinking"
    
    def get_usage_examples(self) -> List[str]:
        return [
            "Analyze a complex business problem",
            "Solve a technical challenge systematically",
            "Make a difficult decision using structured framework",
            "Generate creative solutions to constraints"
        ]

class LearningSkill(BaseSkill):
    """Skill for adaptive learning and knowledge acquisition"""
    
    def __init__(self):
        super().__init__(
            name="Adaptive Learning",
            description="Learns from user interactions and adapts responses based on user preferences and patterns",
            category=SkillCategory.LEARNING
        )
        
        self.parameters = [
            SkillParameter(
                name="learning_mode",
                description="Mode of learning (observe, adapt, teach, review)",
                required=True,
                parameter_type="string"
            ),
            SkillParameter(
                name="subject_area",
                description="Subject area to focus learning on",
                required=False,
                parameter_type="string"
            ),
            SkillParameter(
                name="interaction_history",
                description="Recent interaction history for learning",
                required=False,
                parameter_type="object"
            )
        ]
    
    def execute(self, user_id: str, parameters: Dict[str, Any], 
                context: Optional[Dict[str, Any]] = None) -> SkillResult:
        """Execute learning task"""
        start_time = datetime.now()
        
        try:
            learning_mode = parameters.get("learning_mode", "observe")
            subject_area = parameters.get("subject_area", "general")
            interaction_history = parameters.get("interaction_history", {})
            
            if learning_mode == "observe":
                result = self._observe_patterns(interaction_history, subject_area)
            elif learning_mode == "adapt":
                result = self._adapt_responses(interaction_history, subject_area)
            elif learning_mode == "teach":
                result = self._teach_concept(subject_area, interaction_history)
            elif learning_mode == "review":
                result = self._review_progress(user_id, subject_area)
            else:
                return SkillResult(
                    success=False,
                    error=f"Unknown learning mode: {learning_mode}",
                    execution_time=(datetime.now() - start_time).total_seconds()
                )
            
            execution_time = (datetime.now() - start_time).total_seconds()
            
            return SkillResult(
                success=True,
                data=result,
                message=f"Successfully completed {learning_mode} learning task",
                execution_time=execution_time
            )
            
        except Exception as e:
            execution_time = (datetime.now() - start_time).total_seconds()
            return SkillResult(
                success=False,
                error=str(e),
                execution_time=execution_time
            )
    
    def _observe_patterns(self, history: Dict[str, Any], subject: str) -> Dict[str, Any]:
        """Observe patterns in user interactions"""
        patterns = {
            "communication_style": "conversational",
            "preferred_topics": [subject],
            "interaction_frequency": "regular",
            "response_preferences": "detailed",
            "learning_style": "adaptive"
        }
        return {"observed_patterns": patterns}
    
    def _adapt_responses(self, history: Dict[str, Any], subject: str) -> Dict[str, Any]:
        """Adapt responses based on learned patterns"""
        adaptations = {
            "response_style": "personalized",
            "complexity_level": "adaptive",
            "topic_focus": subject,
            "interaction_tone": "friendly"
        }
        return {"response_adaptations": adaptations}
    
    def _teach_concept(self, subject: str, context: Dict[str, Any]) -> Dict[str, Any]:
        """Teach a concept based on user needs"""
        lesson = {
            "concept": subject,
            "approach": "interactive",
            "examples": ["Example 1", "Example 2"],
            "difficulty": "beginner"
        }
        return {"lesson_plan": lesson}
    
    def _review_progress(self, user_id: str, subject: str) -> Dict[str, Any]:
        """Review learning progress"""
        progress = {
            "subject": subject,
            "mastery_level": "intermediate",
            "areas_for_improvement": ["Advanced concepts", "Practical application"],
            "next_steps": ["Practice exercises", "Real-world application"]
        }
        return {"progress_report": progress}
    
    def get_capability_description(self) -> str:
        return "Adaptive learning that observes user patterns and adapts responses accordingly"
    
    def get_usage_examples(self) -> List[str]:
        return [
            "Learn user communication preferences",
            "Adapt responses to user style",
            "Teach concepts based on user needs",
            "Track learning progress over time"
        ]

class SkillsManager:
    """Manages all available skills and their execution"""
    
    def __init__(self):
        self.skills: Dict[str, BaseSkill] = {}
        self.skill_chains: Dict[str, List[str]] = {}
        self.execution_history: List[SkillExecution] = []
        self._initialize_skills()
        self._initialize_skill_chains()
        logger.info("Skills manager initialized")
    
    def _initialize_skills(self):
        """Initialize all available skills"""
        skills = [
            CalendarManagementSkill(),
            GoalTrackingSkill(),
            ReminderSkill(),
            UtilitySkill(),
            ReasoningSkill(),
            LearningSkill()
        ]
        
        for skill in skills:
            self.skills[skill.name] = skill
            logger.info(f"Registered skill: {skill.name}")
    
    def _initialize_skill_chains(self):
        """Initialize predefined skill chains"""
        self.skill_chains = {
            "morning_routine": ["reminder_management", "goal_tracking", "calendar_management"],
            "goal_review": ["goal_tracking", "reminder_management"],
            "schedule_planning": ["calendar_management", "reminder_management"],
            "wellbeing_check": ["goal_tracking", "reminder_management", "utility_operations"]
        }
        logger.info(f"Initialized {len(self.skill_chains)} skill chains")
    
    def get_skill(self, skill_name: str) -> Optional[BaseSkill]:
        """Get a specific skill by name"""
        return self.skills.get(skill_name)
    
    def list_skills(self, category: Optional[SkillCategory] = None) -> List[BaseSkill]:
        """List all skills, optionally filtered by category"""
        if category:
            return [skill for skill in self.skills.values() if skill.category == category]
        return list(self.skills.values())
    
    def execute_skill(self, skill_name: str, user_id: str, parameters: Dict[str, Any],
                      context: Optional[Dict[str, Any]] = None) -> SkillResult:
        """Execute a single skill"""
        skill = self.get_skill(skill_name)
        if not skill:
            return SkillResult(
                success=False,
                message=f"Skill not found: {skill_name}",
                error="Unknown skill"
            )
        
        # Validate parameters
        is_valid, errors = skill.validate_parameters(parameters)
        if not is_valid:
            return SkillResult(
                success=False,
                message=f"Invalid parameters: {', '.join(errors)}",
                error="Parameter validation failed"
            )
        
        # Create execution record
        execution = SkillExecution(
            execution_id=str(uuid.uuid4()),
            skill_name=skill_name,
            user_id=user_id,
            parameters=parameters,
            start_time=datetime.now(),
            status=SkillStatus.RUNNING,
            context=context
        )
        
        try:
            # Execute the skill
            result = skill.execute(user_id, parameters, context)
            
            # Update execution record
            execution.end_time = datetime.now()
            execution.status = SkillStatus.COMPLETED if result.success else SkillStatus.FAILED
            execution.result = result
            
            # Update skill statistics
            self._update_skill_stats(skill, result)
            
        except Exception as e:
            execution.end_time = datetime.now()
            execution.status = SkillStatus.FAILED
            execution.result = SkillResult(
                success=False,
                message=f"Skill execution failed: {str(e)}",
                error=str(e)
            )
            result = execution.result
        
        # Store execution record
        self.execution_history.append(execution)
        
        return result
    
    def execute_skill_chain(self, chain_name: str, user_id: str, 
                           parameters: Dict[str, Any], context: Optional[Dict[str, Any]] = None) -> List[SkillResult]:
        """Execute a chain of skills in sequence"""
        if chain_name not in self.skill_chains:
            return [SkillResult(
                success=False,
                message=f"Skill chain not found: {chain_name}",
                error="Unknown skill chain"
            )]
        
        skill_names = self.skill_chains[chain_name]
        results = []
        
        for skill_name in skill_names:
            # Execute each skill in the chain
            result = self.execute_skill(skill_name, user_id, parameters, context)
            results.append(result)
            
            # If a skill fails, we might want to stop the chain
            if not result.success:
                logger.warning(f"Skill chain {chain_name} stopped due to failure in {skill_name}")
                break
        
        return results
    
    def suggest_skills(self, user_message: str, user_context: Dict[str, Any]) -> List[Tuple[BaseSkill, float]]:
        """Suggest relevant skills based on user message and context"""
        suggestions = []
        
        for skill in self.skills.values():
            relevance_score = self._calculate_skill_relevance(skill, user_message, user_context)
            if relevance_score > 0.3:  # Threshold for relevance
                suggestions.append((skill, relevance_score))
        
        # Sort by relevance score (highest first)
        suggestions.sort(key=lambda x: x[1], reverse=True)
        
        return suggestions[:5]  # Return top 5 suggestions
    
    def _calculate_skill_relevance(self, skill: BaseSkill, user_message: str, 
                                  user_context: Dict[str, Any]) -> float:
        """Calculate how relevant a skill is to the user's message and context"""
        relevance_score = 0.0
        
        # Check if skill can execute given user context
        if not skill.can_execute(user_context):
            return 0.0
        
        # Keyword matching
        message_lower = user_message.lower()
        skill_name_lower = skill.name.lower()
        skill_desc_lower = skill.description.lower()
        
        # Direct name match
        if skill_name_lower in message_lower:
            relevance_score += 0.8
        
        # Description keyword matching
        keywords = skill_desc_lower.split()
        for keyword in keywords:
            if len(keyword) > 3 and keyword in message_lower:
                relevance_score += 0.2
        
        # Category-based relevance
        if skill.category == SkillCategory.CALENDAR and any(word in message_lower for word in ["schedule", "calendar", "event", "meeting"]):
            relevance_score += 0.4
        elif skill.category == SkillCategory.GOALS and any(word in message_lower for word in ["goal", "target", "progress", "achieve"]):
            relevance_score += 0.4
        elif skill.category == SkillCategory.REMINDERS and any(word in message_lower for word in ["remind", "reminder", "notify", "alert"]):
            relevance_score += 0.4
        elif skill.category == SkillCategory.UTILITY and any(word in message_lower for word in ["time", "weather", "calculate", "convert"]):
            relevance_score += 0.4
        
        # Context-based relevance
        if "goals" in user_context and skill.category == SkillCategory.GOALS:
            relevance_score += 0.3
        if "reminders" in user_context and skill.category == SkillCategory.REMINDERS:
            relevance_score += 0.3
        if "calendar" in user_context and skill.category == SkillCategory.CALENDAR:
            relevance_score += 0.3
        
        return min(relevance_score, 1.0)  # Cap at 1.0
    
    def _update_skill_stats(self, skill: BaseSkill, result: SkillResult):
        """Update skill execution statistics"""
        skill.total_executions += 1
        
        if result.success:
            # Update success rate
            current_successes = skill.success_rate * (skill.total_executions - 1)
            skill.success_rate = (current_successes + 1) / skill.total_executions
            
            # Update average execution time
            if result.execution_time > 0:
                current_total_time = skill.avg_execution_time * (skill.total_executions - 1)
                skill.avg_execution_time = (current_total_time + result.execution_time) / skill.total_executions
    
    def get_execution_stats(self) -> Dict[str, Any]:
        """Get overall execution statistics"""
        total_executions = len(self.execution_history)
        successful_executions = len([e for e in self.execution_history if e.status == SkillStatus.COMPLETED])
        failed_executions = len([e for e in self.execution_history if e.status == SkillStatus.FAILED])
        
        avg_execution_time = 0.0
        if total_executions > 0:
            total_time = sum(e.result.execution_time for e in self.execution_history if e.result)
            avg_execution_time = total_time / total_executions
        
        return {
            "total_executions": total_executions,
            "successful_executions": successful_executions,
            "failed_executions": failed_executions,
            "success_rate": successful_executions / total_executions if total_executions > 0 else 0.0,
            "avg_execution_time": avg_execution_time,
            "skills_used": list(set(e.skill_name for e in self.execution_history))
        }
    
    def get_skill_capabilities(self) -> Dict[str, str]:
        """Get descriptions of all skill capabilities"""
        return {skill.name: skill.get_capability_description() for skill in self.skills.values()}
    
    def get_skill_usage_examples(self) -> Dict[str, List[str]]:
        """Get usage examples for all skills"""
        return {skill.name: skill.get_usage_examples() for skill in self.skills.values()}
