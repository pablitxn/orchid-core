# Orchid Core Development Container
# Includes: .NET 10, Node.js 22, and all development tools

FROM mcr.microsoft.com/devcontainers/dotnet:1-9.0-bookworm

# Arguments for customization
ARG DOTNET_VERSION=10.0
ARG NODE_VERSION=22
ARG INSTALL_AZURE_CLI=false

# Environment variables
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_GENERATE_ASPNET_CERTIFICATE=true \
    NUGET_XMLDOC_MODE=skip \
    PATH="${PATH}:/root/.dotnet/tools:/home/vscode/.dotnet/tools"

# Install .NET 10 SDK (preview/rc)
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet \
    && rm dotnet-install.sh

# Install Node.js 22
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y nodejs \
    && npm install -g pnpm \
    && (which yarn || npm install -g yarn)

# Install additional development tools
RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get -y install --no-install-recommends \
        postgresql-client \
        redis-tools \
        jq \
        httpie \
        ripgrep \
        fd-find \
        bat \
        exa \
        fzf \
        zsh \
        git-lfs \
        # For health checks and debugging
        curl \
        wget \
        netcat-openbsd \
        dnsutils \
        iputils-ping \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install Claude Code CLI (for AI-assisted development)
RUN npm install -g @anthropic-ai/claude-code || echo "Claude Code installation skipped"

# Setup non-root user
USER vscode

# Install global .NET tools as vscode user
RUN dotnet tool install --global dotnet-ef \
    && dotnet tool install --global dotnet-outdated-tool \
    && dotnet tool install --global dotnet-format

# Install oh-my-zsh for better terminal experience (optional)
RUN sh -c "$(curl -fsSL https://raw.githubusercontent.com/ohmyzsh/ohmyzsh/master/tools/install.sh)" "" --unattended || true

# Set working directory
WORKDIR /workspace

# Default command
CMD ["sleep", "infinity"]
