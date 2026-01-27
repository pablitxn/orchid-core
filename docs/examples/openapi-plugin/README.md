# OpenAPI Plugin Example

This is a sample Python plugin server that demonstrates how to create an OpenAPI-compatible service for integration with the Orchid Agent Framework.

## Quick Start

1. **Install dependencies**:
   ```bash
   pip install -r requirements.txt
   ```

2. **Run the server**:
   ```bash
   uvicorn main:app --reload --port 8000
   ```

3. **View the OpenAPI spec**:
   - Swagger UI: http://localhost:8000/docs
   - ReDoc: http://localhost:8000/redoc
   - OpenAPI JSON: http://localhost:8000/openapi.json

## Register with Orchid

Register this plugin via the Admin API:

```bash
curl -X POST http://localhost:5000/api/admin/openapi-plugins \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <admin-token>" \
  -d '{
    "name": "Analysis Plugin",
    "systemName": "analysis_plugin",
    "description": "Text analysis and sentiment detection",
    "baseUrl": "http://localhost:8000",
    "openApiSpecUrl": "/openapi.json",
    "priceCredits": 2,
    "isActive": true
  }'
```

## Available Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/analyze` | Analyze text sentiment |
| GET | `/status` | Get service health status |
| GET | `/items/{item_id}` | Get item by ID |
| POST | `/summarize` | Summarize text |

## Example Usage

Once registered, the AI agent can invoke these functions:

```
User: Can you analyze the sentiment of "I love this amazing product"?

Agent: [Invokes analyzeData function]
Result: { "sentiment": "positive", "confidence": 0.75, "word_count": 5 }
```

## OpenAPI Spec Structure

The key requirements for a compatible OpenAPI spec:

1. **operationId**: Each operation must have a unique `operationId` that becomes the function name
2. **summary/description**: Used by the AI to understand what the function does
3. **parameters**: Defined using OpenAPI parameter objects
4. **requestBody**: For POST/PUT endpoints, define the expected JSON schema

Example operation:

```yaml
paths:
  /analyze:
    post:
      operationId: analyzeData  # Required - becomes the function name
      summary: Analyze input data  # Used by AI for understanding
      requestBody:
        content:
          application/json:
            schema:
              type: object
              properties:
                text:
                  type: string
      responses:
        '200':
          description: Analysis result
```

## Security Considerations

For production deployments:

1. **Authentication**: Add API key or OAuth2 authentication
2. **Rate Limiting**: Implement rate limits to prevent abuse
3. **HTTPS**: Always use HTTPS in production
4. **Input Validation**: Validate all input data thoroughly
5. **Error Handling**: Return appropriate error responses

## License

This example is part of the Orchid Labs Core project.
