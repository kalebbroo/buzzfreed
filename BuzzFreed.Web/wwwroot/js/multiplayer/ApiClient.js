/**
 * ApiClient.js - HTTP API client for backend communication
 *
 * RESPONSIBILITIES:
 * - Make HTTP requests to backend controllers
 * - Handle request/response serialization
 * - Manage error handling and retries
 * - Provide typed API methods
 * - Handle loading states
 *
 * API STRUCTURE:
 * Room API:
 * - createRoom()
 * - joinRoom()
 * - leaveRoom()
 * - setReady()
 * - updateSettings()
 * - createTeams()
 * - assignTeam()
 * - startGame()
 *
 * Game API:
 * - getSession()
 * - submitAnswer()
 * - nextTurn()
 * - endGame()
 * - getLeaderboard()
 * - submitReaction()
 * - submitSuggestion()
 * - submitPrediction()
 * - sendChat()
 *
 * ERROR HANDLING:
 * - Network errors: Retry with exponential backoff
 * - 400 errors: Show user-friendly message
 * - 401/403 errors: Redirect to auth
 * - 500 errors: Show generic error, log details
 *
 * TODO: Add request caching
 * TODO: Add request cancellation
 * TODO: Add request deduplication
 * TODO: Add offline queue
 */

import gameState from './GameState.js';

class ApiClient {
    constructor() {
        this.baseUrl = window.location.origin;
        this.timeout = 30000; // 30 seconds
        this.maxRetries = 3;

        console.log('[ApiClient] Initialized with base URL:', this.baseUrl);
    }

    // ==========================================
    // HTTP HELPERS
    // ==========================================

    /**
     * Make HTTP GET request
     */
    async get(endpoint, options = {}) {
        return this.request('GET', endpoint, null, options);
    }

    /**
     * Make HTTP POST request
     */
    async post(endpoint, data, options = {}) {
        return this.request('POST', endpoint, data, options);
    }

    /**
     * Make HTTP PUT request
     */
    async put(endpoint, data, options = {}) {
        return this.request('PUT', endpoint, data, options);
    }

    /**
     * Make HTTP DELETE request
     */
    async delete(endpoint, options = {}) {
        return this.request('DELETE', endpoint, null, options);
    }

    /**
     * Core request method
     */
    async request(method, endpoint, data = null, options = {}) {
        const url = `${this.baseUrl}${endpoint}`;
        const retries = options.retries || 0;

        console.log(`[ApiClient] ${method} ${endpoint}`);

        try {
            const config = {
                method,
                headers: {
                    'Content-Type': 'application/json',
                    ...options.headers
                }
            };

            if (data) {
                config.body = JSON.stringify(data);
            }

            // Add timeout
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), this.timeout);
            config.signal = controller.signal;

            const response = await fetch(url, config);
            clearTimeout(timeoutId);

            // Handle HTTP errors
            if (!response.ok) {
                const error = await this.handleHttpError(response);

                // Retry on 5xx errors
                if (response.status >= 500 && retries < this.maxRetries) {
                    console.warn(`[ApiClient] Retrying request (${retries + 1}/${this.maxRetries})`);
                    await this.sleep(Math.pow(2, retries) * 1000); // Exponential backoff
                    return this.request(method, endpoint, data, { ...options, retries: retries + 1 });
                }

                throw error;
            }

            // Parse response
            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('application/json')) {
                return await response.json();
            }

            return await response.text();

        } catch (error) {
            // Handle network errors
            if (error.name === 'AbortError') {
                console.error('[ApiClient] Request timeout');
                throw new Error('Request timed out. Please try again.');
            }

            if (error instanceof TypeError && error.message.includes('fetch')) {
                console.error('[ApiClient] Network error');

                // Retry network errors
                if (retries < this.maxRetries) {
                    console.warn(`[ApiClient] Retrying after network error (${retries + 1}/${this.maxRetries})`);
                    await this.sleep(Math.pow(2, retries) * 1000);
                    return this.request(method, endpoint, data, { ...options, retries: retries + 1 });
                }

                throw new Error('Network error. Please check your connection.');
            }

            throw error;
        }
    }

    /**
     * Handle HTTP error responses
     */
    async handleHttpError(response) {
        let errorMessage = `HTTP ${response.status}: ${response.statusText}`;

        try {
            const errorData = await response.json();
            if (errorData.error) {
                errorMessage = errorData.error;
            } else if (errorData.message) {
                errorMessage = errorData.message;
            }
        } catch {
            // Response is not JSON
        }

        console.error('[ApiClient] HTTP error:', errorMessage);

        // Show user-friendly messages
        if (response.status === 400) {
            return new Error(errorMessage || 'Invalid request');
        } else if (response.status === 401 || response.status === 403) {
            return new Error('Not authorized');
        } else if (response.status === 404) {
            return new Error('Resource not found');
        } else if (response.status >= 500) {
            return new Error('Server error. Please try again.');
        }

        return new Error(errorMessage);
    }

    /**
     * Sleep utility
     */
    sleep(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    // ==========================================
    // ROOM API
    // ==========================================

    /**
     * Create a new room
     */
    async createRoom(hostUserId, hostUsername, guildId, gameMode = 'HotSeat') {
        const response = await this.post('/api/room/create', {
            hostUserId,
            hostUsername,
            guildId,
            gameMode
        });

        if (!response.success) {
            throw new Error(response.error || 'Failed to create room');
        }

        return response.room;
    }

    /**
     * Join existing room by code
     */
    async joinRoom(roomCode, userId, username) {
        const response = await this.post('/api/room/join', {
            roomCode,
            userId,
            username
        });

        if (!response.success) {
            throw new Error(response.error || 'Failed to join room');
        }

        return response.room;
    }

    /**
     * Leave room
     */
    async leaveRoom(roomId, userId) {
        const response = await this.post('/api/room/leave', {
            roomId,
            userId
        });

        return response.success;
    }

    /**
     * Set player ready state
     */
    async setReady(roomId, userId, isReady) {
        const response = await this.put(`/api/room/${roomId}/ready`, {
            userId,
            isReady
        });

        return response.success;
    }

    /**
     * Update room settings (host only)
     */
    async updateRoomSettings(roomId, userId, settings) {
        const response = await this.put(`/api/room/${roomId}/settings`, {
            userId,
            settings
        });

        if (!response.success) {
            throw new Error(response.error || 'Failed to update settings');
        }

        return true;
    }

    /**
     * Create teams
     */
    async createTeams(roomId, teamCount) {
        const response = await this.post(`/api/room/${roomId}/teams`, {
            teamCount
        });

        return response.success;
    }

    /**
     * Assign player to team
     */
    async assignTeam(roomId, teamId, userId) {
        const response = await this.put(`/api/room/${roomId}/teams/${teamId}`, {
            userId
        });

        return response.success;
    }

    /**
     * Start game
     */
    async startGame(roomId, userId) {
        const response = await this.post(`/api/room/${roomId}/start`, {
            userId
        });

        if (!response.success) {
            throw new Error(response.error || 'Failed to start game');
        }

        return response.sessionId;
    }

    /**
     * Get room by ID
     */
    async getRoom(roomId) {
        return await this.get(`/api/room/${roomId}`);
    }

    /**
     * Get room by code
     */
    async getRoomByCode(roomCode) {
        return await this.get(`/api/room/code/${roomCode}`);
    }

    /**
     * Get available game modes
     */
    async getGameModes(playerCount = null) {
        const query = playerCount ? `?playerCount=${playerCount}` : '';
        return await this.get(`/api/room/modes${query}`);
    }

    // ==========================================
    // GAME API
    // ==========================================

    /**
     * Get session state
     */
    async getSession(sessionId) {
        return await this.get(`/api/game/${sessionId}`);
    }

    /**
     * Submit answer
     */
    async submitAnswer(sessionId, playerId, answerIndex) {
        const response = await this.post(`/api/game/${sessionId}/answer`, {
            playerId,
            answerIndex
        });

        if (!response.success) {
            throw new Error(response.error || 'Failed to submit answer');
        }

        return true;
    }

    /**
     * Advance to next turn
     */
    async nextTurn(sessionId) {
        const response = await this.post(`/api/game/${sessionId}/next-turn`);

        return response.success;
    }

    /**
     * End game
     */
    async endGame(sessionId) {
        const response = await this.post(`/api/game/${sessionId}/end`);

        return response.success;
    }

    /**
     * Get leaderboard
     */
    async getLeaderboard(sessionId) {
        return await this.get(`/api/game/${sessionId}/leaderboard`);
    }

    /**
     * Get current turn
     */
    async getCurrentTurn(sessionId) {
        return await this.get(`/api/game/${sessionId}/turn`);
    }

    /**
     * Submit reaction
     */
    async submitReaction(sessionId, playerId, reactionType, targetPlayerId) {
        const response = await this.post(`/api/game/${sessionId}/reaction`, {
            playerId,
            reactionType,
            targetPlayerId
        });

        return response.success;
    }

    /**
     * Submit suggestion
     */
    async submitSuggestion(sessionId, playerId, suggestedAnswerIndex, reasoning, targetPlayerId) {
        const response = await this.post(`/api/game/${sessionId}/suggestion`, {
            playerId,
            suggestedAnswerIndex,
            reasoning,
            targetPlayerId
        });

        return response.success;
    }

    /**
     * Submit prediction
     */
    async submitPrediction(sessionId, playerId, predictedAnswerIndex, targetPlayerId) {
        const response = await this.post(`/api/game/${sessionId}/prediction`, {
            playerId,
            predictedAnswerIndex,
            targetPlayerId
        });

        return response.success;
    }

    /**
     * Send chat message
     */
    async sendChat(sessionId, playerId, message, teamId = null) {
        const response = await this.post(`/api/game/${sessionId}/chat`, {
            playerId,
            message,
            teamId
        });

        return response.success;
    }

    /**
     * Get reaction summary for turn
     */
    async getReactionSummary(sessionId, turnId) {
        return await this.get(`/api/game/${sessionId}/turn/${turnId}/reactions`);
    }

    /**
     * Get all interactions for turn
     */
    async getTurnInteractions(sessionId, turnId) {
        return await this.get(`/api/game/${sessionId}/turn/${turnId}/interactions`);
    }

    // TODO: Add batch request support
    // TODO: Add request caching
    // TODO: Add request cancellation
    // TODO: Add offline queue
}

// Export singleton instance
const apiClient = new ApiClient();
export default apiClient;
