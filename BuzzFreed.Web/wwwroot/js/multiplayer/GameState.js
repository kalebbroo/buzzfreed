/**
 * GameState.js - Client-side game state management
 *
 * RESPONSIBILITIES:
 * - Maintain authoritative game state synchronized with server
 * - Handle state updates from real-time events
 * - Provide state queries for UI components
 * - Manage optimistic updates for better UX
 * - Handle reconnection and state recovery
 *
 * STATE STRUCTURE:
 * - room: GameRoom object (lobby state)
 * - session: GameSession object (active game state)
 * - currentPlayer: Local player info
 * - uiState: UI-specific state (modals, animations, etc.)
 *
 * EVENT FLOW:
 * 1. State changes on server
 * 2. Server broadcasts event via Discord SDK
 * 3. Event received in EventHandler.js
 * 4. EventHandler calls GameState.update()
 * 5. GameState updates internal state
 * 6. GameState triggers UI updates
 *
 * OPTIMISTIC UPDATES:
 * - Answer submission: Show answer immediately, revert if rejected
 * - Reactions: Show immediately, confirm with server
 * - Ready state: Toggle immediately, sync with server
 *
 * TODO: Add state persistence (localStorage for reconnection)
 * TODO: Add state validation (ensure consistency)
 * TODO: Add state history (undo/replay)
 * TODO: Add state diff tracking (for efficient updates)
 */

class GameState {
    constructor() {
        // Core state
        this.room = null;              // GameRoom object (lobby)
        this.session = null;           // GameSession object (active game)
        this.currentPlayer = null;     // Local player info

        // UI state
        this.uiState = {
            currentScreen: 'home',     // home, lobby, team-select, game, results
            isLoading: false,
            error: null,
            selectedTeam: null,
            pendingAnswer: null,
            showingResults: false
        };

        // Event listeners
        this.listeners = {
            stateChange: [],           // General state changes
            roomUpdate: [],            // Room state updates
            sessionUpdate: [],         // Session state updates
            turnUpdate: [],            // Turn state updates
            interactionReceived: [],   // Interactions from other players
            errorOccurred: []          // Error events
        };

        // Cache
        this.cache = {
            leaderboard: null,
            reactionSummary: null,
            lastUpdateTime: null
        };

        console.log('[GameState] Initialized');
    }

    // ==========================================
    // STATE INITIALIZATION
    // ==========================================

    /**
     * Initialize with current player info from Discord
     */
    initializePlayer(discordUser) {
        this.currentPlayer = {
            userId: discordUser.id,
            username: discordUser.username,
            avatarUrl: discordUser.avatar ?
                `https://cdn.discordapp.com/avatars/${discordUser.id}/${discordUser.avatar}.png` :
                null,
            guildId: null // Will be set when joining/creating room
        };

        console.log('[GameState] Player initialized:', this.currentPlayer);
        this.emit('stateChange', { type: 'playerInit', player: this.currentPlayer });
    }

    /**
     * Set current room (lobby state)
     */
    setRoom(room) {
        this.room = room;
        this.uiState.currentScreen = 'lobby';

        console.log('[GameState] Room set:', room.roomCode);
        this.emit('roomUpdate', { room: this.room });
        this.emit('stateChange', { type: 'roomSet', room: this.room });

        // TODO: Validate room state
        // TODO: Store in localStorage for reconnection
    }

    /**
     * Set current session (active game state)
     */
    setSession(session) {
        this.session = session;
        this.uiState.currentScreen = 'game';

        console.log('[GameState] Session set:', session.sessionId);
        this.emit('sessionUpdate', { session: this.session });
        this.emit('stateChange', { type: 'sessionSet', session: this.session });

        // TODO: Initialize turn timer
        // TODO: Preload images
        // TODO: Store in localStorage
    }

    /**
     * Clear all state (return to home)
     */
    clear() {
        this.room = null;
        this.session = null;
        this.uiState = {
            currentScreen: 'home',
            isLoading: false,
            error: null,
            selectedTeam: null,
            pendingAnswer: null,
            showingResults: false
        };

        console.log('[GameState] State cleared');
        this.emit('stateChange', { type: 'cleared' });

        // TODO: Clear localStorage
        // TODO: Cleanup event listeners
    }

    // ==========================================
    // STATE QUERIES
    // ==========================================

    /**
     * Is player in a room?
     */
    isInRoom() {
        return this.room !== null;
    }

    /**
     * Is game active?
     */
    isInGame() {
        return this.session !== null && this.session.state === 'Active';
    }

    /**
     * Is current player the host?
     */
    isHost() {
        return this.room && this.currentPlayer &&
               this.room.hostUserId === this.currentPlayer.userId;
    }

    /**
     * Is current player active in current turn?
     */
    isActivePlayer() {
        return this.session &&
               this.session.currentTurn &&
               this.currentPlayer &&
               this.session.currentTurn.activePlayerId === this.currentPlayer.userId;
    }

    /**
     * Get current player's team
     */
    getCurrentTeam() {
        if (!this.room || !this.room.teams || !this.currentPlayer) {
            return null;
        }

        for (let team of Object.values(this.room.teams)) {
            if (team.playerIds.includes(this.currentPlayer.userId)) {
                return team;
            }
        }

        return null;
    }

    /**
     * Get current question
     */
    getCurrentQuestion() {
        if (!this.session || !this.session.currentTurn || !this.session.currentQuiz) {
            return null;
        }

        const questionIndex = this.session.currentTurn.questionNumber - 1;
        return this.session.currentQuiz.questions[questionIndex];
    }

    /**
     * Get player by ID
     */
    getPlayer(playerId) {
        if (this.room) {
            return this.room.players.find(p => p.userId === playerId);
        }

        if (this.session) {
            return this.session.players.find(p => p.userId === playerId);
        }

        return null;
    }

    /**
     * Get current scores
     */
    getScores() {
        if (this.session && this.session.scores) {
            return this.session.scores;
        }

        return {};
    }

    /**
     * Get time remaining in current turn
     */
    getTimeRemaining() {
        if (!this.session || !this.session.currentTurn) {
            return 0;
        }

        const turn = this.session.currentTurn;
        const elapsed = (Date.now() - new Date(turn.startTime).getTime()) / 1000;
        const remaining = Math.max(0, turn.timeLimit - elapsed);

        return remaining;
    }

    // ==========================================
    // STATE UPDATES
    // ==========================================

    /**
     * Update room state (partial update)
     */
    updateRoom(updates) {
        if (!this.room) return;

        Object.assign(this.room, updates);

        console.log('[GameState] Room updated:', updates);
        this.emit('roomUpdate', { room: this.room, updates });

        // TODO: Validate updates
        // TODO: Handle conflicts
    }

    /**
     * Update session state (partial update)
     */
    updateSession(updates) {
        if (!this.session) return;

        Object.assign(this.session, updates);

        console.log('[GameState] Session updated:', updates);
        this.emit('sessionUpdate', { session: this.session, updates });

        // TODO: Handle turn transitions
        // TODO: Update cache
    }

    /**
     * Update current turn state
     */
    updateTurn(turnUpdates) {
        if (!this.session || !this.session.currentTurn) return;

        Object.assign(this.session.currentTurn, turnUpdates);

        console.log('[GameState] Turn updated:', turnUpdates);
        this.emit('turnUpdate', { turn: this.session.currentTurn, updates: turnUpdates });

        // TODO: Handle phase changes
        // TODO: Trigger animations
    }

    /**
     * Add player to room
     */
    addPlayer(player) {
        if (!this.room) return;

        // Check if player already exists
        const exists = this.room.players.some(p => p.userId === player.userId);
        if (exists) return;

        this.room.players.push(player);

        console.log('[GameState] Player joined:', player.username);
        this.emit('roomUpdate', { room: this.room, event: 'playerJoined', player });

        // TODO: Play join animation
        // TODO: Show notification
    }

    /**
     * Remove player from room
     */
    removePlayer(playerId) {
        if (!this.room) return;

        const index = this.room.players.findIndex(p => p.userId === playerId);
        if (index === -1) return;

        const player = this.room.players[index];
        this.room.players.splice(index, 1);

        console.log('[GameState] Player left:', player.username);
        this.emit('roomUpdate', { room: this.room, event: 'playerLeft', player });

        // TODO: Handle host transfer
        // TODO: Show notification
    }

    /**
     * Add interaction to current turn
     */
    addInteraction(interaction) {
        if (!this.session || !this.session.currentTurn) return;

        // Add to appropriate list based on type
        switch (interaction.type) {
            case 'reaction':
                this.session.currentTurn.reactions.push(interaction);
                break;
            case 'suggestion':
                this.session.currentTurn.suggestions.push(interaction);
                break;
            case 'prediction':
                this.session.currentTurn.predictions.push(interaction);
                break;
        }

        console.log('[GameState] Interaction added:', interaction);
        this.emit('interactionReceived', { interaction });

        // TODO: Trigger interaction animations
        // TODO: Update interaction counts
    }

    // ==========================================
    // OPTIMISTIC UPDATES
    // ==========================================

    /**
     * Optimistically update ready state
     * Will be reverted if server rejects
     */
    optimisticSetReady(isReady) {
        if (!this.room || !this.currentPlayer) return;

        const player = this.room.players.find(p => p.userId === this.currentPlayer.userId);
        if (!player) return;

        const oldValue = player.isReady;
        player.isReady = isReady;

        console.log('[GameState] Optimistic ready update:', isReady);
        this.emit('roomUpdate', { room: this.room });

        // TODO: Set revert timer
        // TODO: Revert if not confirmed by server
    }

    /**
     * Optimistically submit answer
     */
    optimisticSubmitAnswer(answerIndex) {
        if (!this.session || !this.session.currentTurn) return;

        this.uiState.pendingAnswer = answerIndex;

        console.log('[GameState] Optimistic answer submission:', answerIndex);
        this.emit('turnUpdate', { turn: this.session.currentTurn });

        // TODO: Disable answer buttons
        // TODO: Show loading state
        // TODO: Revert if rejected
    }

    /**
     * Optimistically add reaction
     */
    optimisticAddReaction(reactionType, targetPlayerId) {
        const reaction = {
            playerId: this.currentPlayer.userId,
            type: reactionType,
            targetPlayerId: targetPlayerId,
            timestamp: new Date().toISOString()
        };

        this.addInteraction({ ...reaction, type: 'reaction' });

        console.log('[GameState] Optimistic reaction:', reactionType);

        // TODO: Show reaction animation
        // TODO: Revert if rejected
    }

    // ==========================================
    // ERROR HANDLING
    // ==========================================

    /**
     * Handle error
     */
    setError(error) {
        this.uiState.error = error;
        this.uiState.isLoading = false;

        console.error('[GameState] Error:', error);
        this.emit('errorOccurred', { error });

        // TODO: Show error modal
        // TODO: Auto-clear after timeout
    }

    /**
     * Clear error
     */
    clearError() {
        this.uiState.error = null;
        this.emit('stateChange', { type: 'errorCleared' });
    }

    // ==========================================
    // EVENT SYSTEM
    // ==========================================

    /**
     * Register event listener
     */
    on(eventName, callback) {
        if (!this.listeners[eventName]) {
            console.warn('[GameState] Unknown event:', eventName);
            return;
        }

        this.listeners[eventName].push(callback);
    }

    /**
     * Unregister event listener
     */
    off(eventName, callback) {
        if (!this.listeners[eventName]) return;

        const index = this.listeners[eventName].indexOf(callback);
        if (index > -1) {
            this.listeners[eventName].splice(index, 1);
        }
    }

    /**
     * Emit event to all listeners
     */
    emit(eventName, data) {
        if (!this.listeners[eventName]) return;

        for (let callback of this.listeners[eventName]) {
            try {
                callback(data);
            } catch (error) {
                console.error('[GameState] Listener error:', error);
            }
        }
    }

    // TODO: Add state persistence methods
    // TODO: Add state validation methods
    // TODO: Add reconnection handling
    // TODO: Add state diff/patch methods
}

// Export singleton instance
const gameState = new GameState();
export default gameState;
