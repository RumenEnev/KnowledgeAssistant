# syntax=docker/dockerfile:1
# Build context is this folder (KnowledgeAssistant.Web):
#   docker build -t knowledgeassistant-web .

FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

FROM nginx:1.27-alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/knowledge-assistant.web/browser /usr/share/nginx/html

EXPOSE 80
