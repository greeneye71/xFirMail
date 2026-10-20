# xFirMail, EUPL-1.2. Build from the repository root; no configuration in build context.
FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /source
COPY Directory.Build.props global.json NuGet.Config ./
COPY src/Xfir.Core/ src/Xfir.Core/
COPY src/Xfir.Rendering/ src/Xfir.Rendering/
COPY src/Shared/ src/Shared/
COPY src/Xfir.Cli/ src/Xfir.Cli/
COPY src/Xfir.Mail/ src/Xfir.Mail/
RUN dotnet restore src/Xfir.Cli --locked-mode -p:SecurityAuditRequired=true \
    && dotnet publish src/Xfir.Cli -c Release -r linux-x64 --self-contained true --no-restore -o /out/cli
RUN dotnet restore src/Xfir.Mail --locked-mode -p:SecurityAuditRequired=true \
    && dotnet publish src/Xfir.Mail -c Release -r linux-x64 --self-contained true --no-restore -o /out/mail

FROM registry.fedoraproject.org/fedora:44
ARG VCS_REF=unknown
LABEL org.opencontainers.image.title="xFirMail" \
      org.opencontainers.image.source="https://github.com/greeneye71/xFirMail" \
      org.opencontainers.image.licenses="EUPL-1.2" \
      org.opencontainers.image.revision=$VCS_REF
RUN dnf -y install ca-certificates tzdata glibc-langpack-it libicu openssl-libs krb5-libs zlib libstdc++ \
    && dnf clean all \
    && mkdir -p /app /data /work /config \
    && chown 10001:10001 /data /work
COPY --from=build /out/cli /app/cli
COPY --from=build /out/mail /app/mail
COPY LICENSE LICENSE-IT.txt THIRD-PARTY-NOTICES.md /app/
COPY third-party/ /app/third-party/
USER 10001:10001
WORKDIR /work
ENTRYPOINT ["/app/mail/Xfir.Mail"]
CMD ["/config/appsettings.json"]
