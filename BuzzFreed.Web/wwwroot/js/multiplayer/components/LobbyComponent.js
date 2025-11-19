/**
 * LobbyComponent.js - Room lobby UI component
 *
 * RESPONSIBILITIES:
 * - Display room code and join instructions
 * - Show player list with ready states
 * - Display and manage team assignments (if team mode)
 * - Show quiz customization settings
 * - Handle ready button toggle
 * - Handle start game button (host only)
 * - Show player count and mode requirements
 *
 * UI ELEMENTS:
 * - Room code display (large, copyable)
 * - Player list with avatars and ready indicators
 * - Team assignment UI (if team mode)
 * - Quiz settings panel (expandable)
 * - Ready button (toggle)
 * - Start game button (host only, disabled until requirements met)
 * - Leave room button
 *
 * DYNAMIC BEHAVIOR:
 * - Real-time player list updates
 * - Ready state animations
 * - Start button enabled only when:
 *   - User is host
 *   - Enough players for mode
 *   - All players ready (or host forces)
 * - Team balance indicators
 *
 * TODO: Add room password UI
 * TODO: Add kick player button (host only)
 * TODO: Add transfer host button
 * TODO: Add invite link generation
 * TODO: Add voice channel integration
 */

import gameState from '../GameState.js';
import apiClient from '../ApiClient.js';

class LobbyComponent {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        this.isRendered = false;

        // Bind event handlers
        this.handleReadyClick = this.handleReadyClick.bind(this);
        this.handleStartClick = this.handleStartClick.bind(this);
        this.handleLeaveClick = this.handleLeaveClick.bind(this);
        this.handleSettingsChange = this.handleSettingsChange.bind(this);
        this.handleTeamSelect = this.handleTeamSelect.bind(this);

        // Subscribe to game state updates
        gameState.on('roomUpdate', this.onRoomUpdate.bind(this));

        console.log('[LobbyComponent] Initialized');
    }

    /**
     * Render lobby UI
     */
    render() {
        if (!gameState.room) {
            console.error('[LobbyComponent] No room to render');
            return;
        }

        const room = gameState.room;
        const isHost = gameState.isHost();
        const currentPlayer = gameState.currentPlayer;

        this.container.innerHTML = `
            <div class="lobby">
                <!-- Room Header -->
                <div class="lobby-header">
                    <h1>Room Lobby</h1>
                    <div class="room-code-container">
                        <span class="label">Room Code:</span>
                        <div class="room-code" onclick="copyRoomCode()">
                            ${room.roomCode}
                            <span class="copy-icon">📋</span>
                        </div>
                    </div>
                </div>

                <!-- Mode Info -->
                <div class="mode-info">
                    <h3>${this.getModeName(room.gameMode)}</h3>
                    <p>${this.getModeDescription(room.gameMode)}</p>
                    <div class="player-count">
                        ${room.players.length}/${room.maxPlayers} Players
                    </div>
                </div>

                <!-- Team Assignment (if team mode) -->
                ${this.renderTeamSection()}

                <!-- Player List -->
                <div class="player-list">
                    <h3>Players</h3>
                    ${this.renderPlayerList()}
                </div>

                <!-- Quiz Settings (host only) -->
                ${isHost ? this.renderSettingsPanel() : this.renderSettingsPreview()}

                <!-- Actions -->
                <div class="lobby-actions">
                    <!-- Ready Button -->
                    <button
                        id="ready-btn"
                        class="btn ${this.isPlayerReady(currentPlayer.userId) ? 'btn-ready' : 'btn-not-ready'}"
                        onclick="lobbyComponent.handleReadyClick()">
                        ${this.isPlayerReady(currentPlayer.userId) ? 'Ready!' : 'Not Ready'}
                    </button>

                    <!-- Start Button (host only) -->
                    ${isHost ? `
                        <button
                            id="start-btn"
                            class="btn btn-primary"
                            ${this.canStartGame() ? '' : 'disabled'}
                            onclick="lobbyComponent.handleStartClick()">
                            Start Game
                        </button>
                    ` : ''}

                    <!-- Leave Button -->
                    <button
                        class="btn btn-secondary"
                        onclick="lobbyComponent.handleLeaveClick()">
                        Leave Room
                    </button>
                </div>

                <!-- Requirements Status -->
                ${!this.canStartGame() ? this.renderRequirements() : ''}
            </div>
        `;

        this.isRendered = true;

        // TODO: Setup event listeners
        // TODO: Initialize animations
        // TODO: Start player count animation
    }

    /**
     * Render team assignment section
     */
    renderTeamSection() {
        // TODO: Check if mode requires teams
        const requiresTeams = false; // Get from mode config

        if (!requiresTeams) {
            return '';
        }

        const room = gameState.room;
        const teams = room.teams || {};

        return `
            <div class="team-section">
                <h3>Team Assignment</h3>
                <div class="teams">
                    ${Object.values(teams).map(team => `
                        <div class="team team-${team.color.toLowerCase()}"
                             onclick="lobbyComponent.handleTeamSelect('${team.teamId}')">
                            <div class="team-header">
                                <span class="team-name">${team.name}</span>
                                <span class="team-count">${team.playerIds.length} players</span>
                            </div>
                            <div class="team-players">
                                ${team.playerIds.map(playerId => {
                                    const player = this.getPlayer(playerId);
                                    return player ? `
                                        <div class="team-player">
                                            <img src="${player.avatarUrl || '/img/default-avatar.png'}"
                                                 alt="${player.username}">
                                            <span>${player.username}</span>
                                        </div>
                                    ` : '';
                                }).join('')}
                            </div>
                        </div>
                    `).join('')}
                </div>
            </div>
        `;

        // TODO: Add team creation UI for host
        // TODO: Add team balance indicator
        // TODO: Add auto-balance button
    }

    /**
     * Render player list
     */
    renderPlayerList() {
        const room = gameState.room;

        return room.players.map(player => `
            <div class="player-item ${player.isReady ? 'ready' : ''} ${player.role === 'Host' ? 'host' : ''}">
                <img src="${player.avatarUrl || '/img/default-avatar.png'}"
                     alt="${player.username}"
                     class="player-avatar">
                <div class="player-info">
                    <span class="player-name">
                        ${player.username}
                        ${player.role === 'Host' ? '<span class="host-badge">👑 Host</span>' : ''}
                    </span>
                </div>
                <div class="player-status">
                    ${player.isReady ?
                        '<span class="ready-indicator">✓ Ready</span>' :
                        '<span class="not-ready-indicator">⏳ Not Ready</span>'}
                </div>
            </div>
        `).join('');

        // TODO: Add player animations
        // TODO: Add kick button for host
        // TODO: Add player stats tooltip
    }

    /**
     * Render settings panel (host only)
     */
    renderSettingsPanel() {
        const room = gameState.room;
        const settings = room.quizSettings;

        return `
            <div class="settings-panel">
                <h3>Quiz Settings</h3>
                <div class="settings-form">
                    <div class="form-group">
                        <label>Topic</label>
                        <input type="text"
                               id="setting-topic"
                               value="${settings.topic || ''}"
                               placeholder="Any topic (e.g., Movies, Science)">
                    </div>

                    <div class="form-group">
                        <label>Number of Questions</label>
                        <input type="number"
                               id="setting-question-count"
                               value="${settings.questionCount || 10}"
                               min="5"
                               max="20">
                    </div>

                    <div class="form-group">
                        <label>Include Images?</label>
                        <input type="checkbox"
                               id="setting-include-images"
                               ${settings.includeImages ? 'checked' : ''}>
                    </div>

                    <button class="btn btn-sm" onclick="lobbyComponent.handleSettingsChange()">
                        Update Settings
                    </button>
                </div>
            </div>
        `;

        // TODO: Add more customization options
        // TODO: Add preset templates
        // TODO: Add AI style selector
        // TODO: Add difficulty selector
    }

    /**
     * Render settings preview (non-host)
     */
    renderSettingsPreview() {
        const room = gameState.room;
        const settings = room.quizSettings;

        return `
            <div class="settings-preview">
                <h3>Quiz Settings</h3>
                <div class="settings-info">
                    <div><strong>Topic:</strong> ${settings.topic || 'Random'}</div>
                    <div><strong>Questions:</strong> ${settings.questionCount || 10}</div>
                    <div><strong>Images:</strong> ${settings.includeImages ? 'Yes' : 'No'}</div>
                </div>
            </div>
        `;
    }

    /**
     * Render start requirements
     */
    renderRequirements() {
        const requirements = this.getStartRequirements();

        return `
            <div class="requirements">
                <h4>Requirements to Start:</h4>
                <ul>
                    ${requirements.map(req => `
                        <li class="${req.met ? 'met' : 'unmet'}">
                            ${req.met ? '✓' : '✗'} ${req.text}
                        </li>
                    `).join('')}
                </ul>
            </div>
        `;
    }

    // ==========================================
    // EVENT HANDLERS
    // ==========================================

    /**
     * Handle ready button click
     */
    async handleReadyClick() {
        const currentPlayer = gameState.currentPlayer;
        const isReady = this.isPlayerReady(currentPlayer.userId);

        // Optimistic update
        gameState.optimisticSetReady(!isReady);

        try {
            await apiClient.setReady(
                gameState.room.roomId,
                currentPlayer.userId,
                !isReady
            );

            console.log('[LobbyComponent] Ready state updated');
        } catch (error) {
            console.error('[LobbyComponent] Failed to set ready:', error);
            gameState.setError(error.message);

            // Revert optimistic update
            gameState.optimisticSetReady(isReady);
        }
    }

    /**
     * Handle start game button click
     */
    async handleStartClick() {
        if (!gameState.isHost()) {
            console.warn('[LobbyComponent] Not host');
            return;
        }

        if (!this.canStartGame()) {
            console.warn('[LobbyComponent] Cannot start game yet');
            return;
        }

        gameState.uiState.isLoading = true;
        this.updateStartButton('Starting...');

        try {
            const sessionId = await apiClient.startGame(
                gameState.room.roomId,
                gameState.currentPlayer.userId
            );

            console.log('[LobbyComponent] Game started:', sessionId);

            // TODO: Transition to game screen
            // TODO: Show loading screen
        } catch (error) {
            console.error('[LobbyComponent] Failed to start game:', error);
            gameState.setError(error.message);
            gameState.uiState.isLoading = false;
            this.updateStartButton('Start Game');
        }
    }

    /**
     * Handle leave room button click
     */
    async handleLeaveClick() {
        if (!confirm('Are you sure you want to leave?')) {
            return;
        }

        try {
            await apiClient.leaveRoom(
                gameState.room.roomId,
                gameState.currentPlayer.userId
            );

            console.log('[LobbyComponent] Left room');

            // Clear state and return to home
            gameState.clear();

            // TODO: Navigate to home screen
        } catch (error) {
            console.error('[LobbyComponent] Failed to leave room:', error);
            gameState.setError(error.message);
        }
    }

    /**
     * Handle settings change
     */
    async handleSettingsChange() {
        if (!gameState.isHost()) return;

        const settings = {
            topic: document.getElementById('setting-topic').value,
            questionCount: parseInt(document.getElementById('setting-question-count').value),
            includeImages: document.getElementById('setting-include-images').checked
        };

        try {
            await apiClient.updateRoomSettings(
                gameState.room.roomId,
                gameState.currentPlayer.userId,
                settings
            );

            console.log('[LobbyComponent] Settings updated');

            // TODO: Show success notification
        } catch (error) {
            console.error('[LobbyComponent] Failed to update settings:', error);
            gameState.setError(error.message);
        }
    }

    /**
     * Handle team selection
     */
    async handleTeamSelect(teamId) {
        try {
            await apiClient.assignTeam(
                gameState.room.roomId,
                teamId,
                gameState.currentPlayer.userId
            );

            console.log('[LobbyComponent] Team assigned');
        } catch (error) {
            console.error('[LobbyComponent] Failed to assign team:', error);
            gameState.setError(error.message);
        }
    }

    /**
     * Handle room updates from game state
     */
    onRoomUpdate(data) {
        if (this.isRendered) {
            this.render(); // Re-render on updates
        }
    }

    // ==========================================
    // HELPER METHODS
    // ==========================================

    isPlayerReady(playerId) {
        const player = gameState.room?.players.find(p => p.userId === playerId);
        return player?.isReady || false;
    }

    getPlayer(playerId) {
        return gameState.room?.players.find(p => p.userId === playerId);
    }

    canStartGame() {
        if (!gameState.isHost()) return false;
        if (!gameState.room) return false;

        const requirements = this.getStartRequirements();
        return requirements.every(r => r.met);
    }

    getStartRequirements() {
        const room = gameState.room;

        return [
            {
                text: `At least 2 players`,
                met: room.players.length >= 2 // TODO: Get from mode
            },
            {
                text: `All players ready`,
                met: room.players.every(p => p.isReady || p.role === 'Host')
            }
        ];
    }

    getModeName(modeType) {
        // TODO: Get from mode registry
        return modeType;
    }

    getModeDescription(modeType) {
        // TODO: Get from mode registry
        return 'Take turns answering questions!';
    }

    updateStartButton(text) {
        const btn = document.getElementById('start-btn');
        if (btn) {
            btn.textContent = text;
        }
    }

    destroy() {
        this.isRendered = false;
        // TODO: Cleanup event listeners
        // TODO: Stop animations
    }
}

// Export
export default LobbyComponent;

// TODO: Create HomeComponent (create/join room)
// TODO: Create TeamSelectComponent (team formation)
// TODO: Create GameComponent (active gameplay)
// TODO: Create SpectatorComponent (spectator view with interactions)
// TODO: Create ResultsComponent (turn results and final results)
// TODO: Create LeaderboardComponent (live scores)
