FROM python:3.11-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    gcc \
    postgresql-client \
    portaudio19-dev \
    python3-dev \
    libasound2-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements first for better caching
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY . .

# Create data directory
RUN mkdir -p data/raw

# Expose port
EXPOSE 8077

# Run the application
CMD ["uvicorn", "rag_api:app", "--host", "0.0.0.0", "--port", "8077"]