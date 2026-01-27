"""
Sample OpenAPI Plugin Server

This is an example FastAPI server that exposes an OpenAPI specification
compatible with the Orchid Agent Framework's OpenAPI plugin system.

Usage:
    pip install fastapi uvicorn
    uvicorn main:app --reload --port 8000

Then register this plugin via the Admin API:
    POST /api/admin/openapi-plugins
    {
        "name": "Analysis Plugin",
        "systemName": "analysis_plugin",
        "description": "Text analysis and sentiment detection",
        "baseUrl": "http://localhost:8000",
        "openApiSpecUrl": "/openapi.json",
        "priceCredits": 2,
        "isActive": true
    }
"""

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Optional
import random

app = FastAPI(
    title="Analysis Plugin",
    description="A sample plugin for text analysis",
    version="1.0.0"
)


class AnalyzeRequest(BaseModel):
    """Request body for the analyze endpoint."""
    text: str


class AnalyzeResponse(BaseModel):
    """Response from the analyze endpoint."""
    result: str
    sentiment: str
    confidence: float
    word_count: int


class StatusResponse(BaseModel):
    """Response from the status endpoint."""
    status: str
    version: str
    uptime_seconds: int


class ItemResponse(BaseModel):
    """Response from the get item endpoint."""
    id: str
    name: str
    description: str
    price: float


@app.post("/analyze", response_model=AnalyzeResponse, tags=["Analysis"])
async def analyze_data(request: AnalyzeRequest) -> AnalyzeResponse:
    """
    Analyze input text and return sentiment analysis.

    This endpoint performs basic text analysis including:
    - Sentiment detection (positive, negative, neutral)
    - Confidence score
    - Word count
    """
    text = request.text.lower()

    # Simple sentiment analysis (for demonstration)
    positive_words = {"good", "great", "excellent", "amazing", "wonderful", "happy", "love"}
    negative_words = {"bad", "terrible", "awful", "horrible", "sad", "hate", "poor"}

    words = text.split()
    word_count = len(words)

    positive_count = sum(1 for word in words if word in positive_words)
    negative_count = sum(1 for word in words if word in negative_words)

    if positive_count > negative_count:
        sentiment = "positive"
        confidence = min(0.5 + (positive_count * 0.1), 0.95)
    elif negative_count > positive_count:
        sentiment = "negative"
        confidence = min(0.5 + (negative_count * 0.1), 0.95)
    else:
        sentiment = "neutral"
        confidence = 0.5

    return AnalyzeResponse(
        result="analyzed",
        sentiment=sentiment,
        confidence=round(confidence, 2),
        word_count=word_count
    )


@app.get("/status", response_model=StatusResponse, tags=["Health"])
async def get_status() -> StatusResponse:
    """
    Get the current service status.

    Returns the health status, version, and uptime of the service.
    """
    return StatusResponse(
        status="healthy",
        version="1.0.0",
        uptime_seconds=random.randint(1000, 100000)
    )


@app.get("/items/{item_id}", response_model=ItemResponse, tags=["Items"])
async def get_item(item_id: str) -> ItemResponse:
    """
    Get item details by ID.

    Retrieves the details of a specific item from the catalog.
    """
    # Simulated item data
    items = {
        "item-001": ItemResponse(
            id="item-001",
            name="Widget Pro",
            description="A professional-grade widget",
            price=29.99
        ),
        "item-002": ItemResponse(
            id="item-002",
            name="Gadget Plus",
            description="An enhanced gadget with extra features",
            price=49.99
        ),
        "item-003": ItemResponse(
            id="item-003",
            name="Tool Master",
            description="The ultimate tool for any job",
            price=99.99
        ),
    }

    if item_id not in items:
        # Return a generic item for any other ID
        return ItemResponse(
            id=item_id,
            name=f"Item {item_id}",
            description="A sample item",
            price=19.99
        )

    return items[item_id]


@app.post("/summarize", tags=["Analysis"])
async def summarize_text(request: AnalyzeRequest) -> dict:
    """
    Summarize the input text.

    Generates a brief summary of the provided text.
    """
    words = request.text.split()
    word_count = len(words)

    if word_count <= 10:
        summary = request.text
    else:
        # Take first 10 words as "summary" (simplified)
        summary = " ".join(words[:10]) + "..."

    return {
        "original_length": word_count,
        "summary": summary,
        "compression_ratio": round(min(10, word_count) / max(word_count, 1), 2)
    }


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
