# OpenAI Proxy Server

This tiny server keeps the OpenAI API key outside the Unity project.

## Setup

1. Copy `.env.example` to `.env`.
2. Put the real OpenAI key in `.env`.
3. Run the server:

```powershell
node server.js
```

The Unity project sends requests to:

```text
http://127.0.0.1:8787/openai/responses
```

For another computer to use the same shared server, change Unity's `openAiProxyUrl` to that server's reachable URL.

Do not commit `.env`.
