# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY lib lib
COPY src src
RUN dotnet publish src/Panda/Panda.csproj -c Release -o /app -p:DebugType=none -nologo

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim
ARG TARGETARCH
RUN apt-get update \
	&& apt-get install -y --no-install-recommends ffmpeg curl unzip ca-certificates \
	&& rm -rf /var/lib/apt/lists/* \
	&& case "$TARGETARCH" in \
		arm64) YTDLP=yt-dlp_linux_aarch64; DENO=deno-aarch64-unknown-linux-gnu.zip ;; \
		*) YTDLP=yt-dlp_linux; DENO=deno-x86_64-unknown-linux-gnu.zip ;; \
	esac \
	&& curl -fsSL "https://github.com/yt-dlp/yt-dlp/releases/latest/download/$YTDLP" -o /usr/local/bin/yt-dlp \
	&& curl -fsSL "https://github.com/denoland/deno/releases/latest/download/$DENO" -o /tmp/deno.zip \
	&& unzip -q /tmp/deno.zip -d /usr/local/bin && rm /tmp/deno.zip \
	&& chmod 755 /usr/local/bin/yt-dlp /usr/local/bin/deno \
	&& useradd --system --create-home --home-dir /home/panda panda \
	&& mkdir /data && chown panda /data

COPY --from=build /app /app
USER panda
ENV PANDA_DATA=/data
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "/app/panda.dll"]
