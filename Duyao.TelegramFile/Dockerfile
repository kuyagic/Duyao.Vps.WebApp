FROM debian:bookworm
RUN apt update && apt install -y libicu-dev
COPY ./app /app
ENTRYPOINT [ "/app/corefilebot" ]
EXPOSE 5000