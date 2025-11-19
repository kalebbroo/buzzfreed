# BuzzFreed - Multiplayer AI Quiz Party Game
## Comprehensive Design Document

---

## 🎯 Core Vision
Transform BuzzFeed-style AI quizzes into an engaging multiplayer party game where players form teams, compete, interact in real-time, and experience dynamic AI-generated content together.

---

## 🎮 Game Modes

### 1. **Hot Seat** (1 Active Player, Others Spectate)
**Flow:**
- One player is "in the hot seat"
- Others watch and react in real-time
- Spectators can send suggestions (player can see count, not content until after)
- After each answer, brief reaction phase
- Rotate through all players

**Engagement:**
- Reaction emojis (😂 🤔 😱 👍 👎)
- Prediction system: "What will they pick?"
- Anonymous suggestions that player sees after answering
- Vote on funniest answer combinations

**Scoring:**
- Accuracy bonus for personality match
- Speed bonus
- Crowd favorite bonus (most reactions)

---

### 2. **Team Challenge** (Team vs Team)
**Flow:**
- 2-4 teams compete
- Teams take turns answering questions
- Team members vote on answer (majority wins)
- Time limit for team discussion

**Engagement:**
- Team chat during decision time
- Captain system (rotates each round)
- Steal mechanic: If team gets it "wrong" (based on AI reasoning), others can steal
- Power-ups: Skip question, double points, sabotage

**Scoring:**
- Correct answers based on AI personality analysis
- Speed bonus
- Consensus bonus (unanimous team vote)
- Steal points

---

### 3. **Guess the Player** (Social Deduction)
**Flow:**
- Random player selected (hidden from others)
- AI generates quiz for that player's "personality"
- Others guess who the quiz was made for
- Reveal answers and actual player

**Engagement:**
- Players write guesses
- Point for correct guess
- Funny "how well do you know each other" moments

**Scoring:**
- Correct guesses: 100 points
- Close guesses (2nd/3rd place votes): 50/25 points
- Bonus if everyone guesses correctly (team bonding!)

---

### 4. **Speed Round** (All Players Compete)
**Flow:**
- Same question shown to all players simultaneously
- First to answer gets points
- 10 rapid-fire questions
- Leaderboard updates in real-time

**Engagement:**
- Live leaderboard
- Combo multipliers (3+ correct in a row)
- Lightning round music/effects

**Scoring:**
- First place: 100 pts
- Second: 75 pts
- Third: 50 pts
- Participation: 25 pts
- Combo multiplier: x2, x3, x5

---

### 5. **Collaborative Story** (Everyone Together)
**Flow:**
- AI generates a story-based quiz
- Each question, players vote together
- Story branches based on collective choices
- Final personality is group's combined vibe

**Engagement:**
- Discussion time between questions
- See how each choice affects story branch
- Shared responsibility for outcome

**Scoring:**
- No competitive scoring
- Unlock story achievements
- Funny group personality results

---

### 6. **Sabotage** (Chaos Mode)
**Flow:**
- Players answer their own quizzes
- BUT: Others can activate sabotages
- Limited sabotage points (use wisely)
- Sabotages: flip answers, time pressure, fake options, confusion

**Engagement:**
- Strategic sabotage usage
- Alliances and betrayals
- Revenge mechanic (counter-sabotage)

**Scoring:**
- Base points for correct answers
- Bonus for surviving sabotages
- Points for successful sabotages

---

## 🎨 Quiz Customization System

### Player-Controlled Parameters:

**Topic Generation:**
- Random (AI decides)
- Category selection (Food, Movies, Gaming, Personality, Lifestyle)
- Custom prompt ("Make it about 90s nostalgia")
- Difficulty: Casual, Challenging, Absurd

**Question Style:**
- Classic BuzzFeed (light and fun)
- Deep & Philosophical
- Chaotic/Absurd
- Rapid-fire (short questions)
- Story-driven (narrative questions)

**Image Generation:**
- Style: Realistic, Cartoon, Anime, Abstract, Meme, Retro
- Mood: Cheerful, Dark, Energetic, Calm
- Include images: Yes, Text-only, Images on results only

**Result Presentation:**
- Personality types: 4, 6, 8 options
- Result depth: Quick summary, Detailed analysis, Roast mode
- Include AI image: Yes/No
- Shareable format: Image, Text, Both

---

## 🎭 User Roles & Interactions

### Room Creator (Host)
- Choose game mode
- Set quiz parameters
- Start game
- Can skip/restart

### Team Captain (Rotates)
- Final say in Team Challenge
- Can request team vote
- Manages team strategy

### Active Player (Hot Seat)
- Answers quiz questions
- Sees suggestion counts (not content)
- Can request hint (costs points)

### Spectator
- React with emojis (limited per question)
- Send suggestions (anonymous, limited)
- Predict answers
- Vote in team modes

---

## 🖼️ UI/UX Design

### Lobby Screen
```
┌─────────────────────────────────────────┐
│  BuzzFreed Party 🎉                     │
│                                          │
│  Room Code: ABC-123    [📋 Copy]        │
│                                          │
│  ┌─────────────────────────────────┐   │
│  │ Players (4/8)                    │   │
│  │ 👤 Player1 (Host) ⭐             │   │
│  │ 👤 Player2                       │   │
│  │ 👤 Player3                       │   │
│  │ 👤 Player4                       │   │
│  └─────────────────────────────────┘   │
│                                          │
│  Game Mode: [▼ Hot Seat          ]     │
│                                          │
│  Quiz Settings:                          │
│  Topic: [▼ Random AI         ]          │
│  Style: [▼ Classic BuzzFeed  ]          │
│  Images: [▼ AI Generated     ]          │
│  Length: [▼ 6 Questions      ]          │
│                                          │
│  [        Start Game 🚀        ]        │
└─────────────────────────────────────────┘
```

### Team Formation (Team Challenge Mode)
```
┌─────────────────────────────────────────┐
│  Form Teams                              │
│                                          │
│  🔴 Team Red (2)     🔵 Team Blue (2)   │
│  ┌────────────┐     ┌────────────┐     │
│  │ Player1 ⭐ │     │ Player3     │     │
│  │ Player2    │     │ Player4     │     │
│  └────────────┘     └────────────┘     │
│                                          │
│  🟢 Team Green (0)   🟡 Team Yellow (0) │
│  ┌────────────┐     ┌────────────┐     │
│  │            │     │            │     │
│  │  [Empty]   │     │  [Empty]   │     │
│  └────────────┘     └────────────┘     │
│                                          │
│  [Auto Balance] [Randomize] [Ready✓]   │
└─────────────────────────────────────────┘
```

### Active Player View (Hot Seat)
```
┌─────────────────────────────────────────┐
│  Question 3/6          ⏱️ 0:24          │
│  ━━━━━━━━━━━━━━━━░░░░░░ 50%            │
│                                          │
│  Which dessert matches your vibe?       │
│                                          │
│  ┌─────────────────────────────────┐   │
│  │ [🍰] Chocolate Cake              │   │
│  │      Rich and indulgent          │   │
│  └─────────────────────────────────┘   │
│  ┌─────────────────────────────────┐   │
│  │ [🍨] Vanilla Ice Cream           │   │
│  │      Classic and refreshing      │   │
│  └─────────────────────────────────┘   │
│  ┌─────────────────────────────────┐   │
│  │ [🥧] Apple Pie                   │   │
│  │      Warm and comforting         │   │
│  └─────────────────────────────────┘   │
│  ┌─────────────────────────────────┐   │
│  │ [🍩] Donuts                      │   │
│  │      Fun and spontaneous         │   │
│  └─────────────────────────────────┘   │
│                                          │
│  💡 Suggestions: 3 [Show After Answer]  │
│                                          │
│  Spectators: 😂×5 🤔×3 😱×2            │
└─────────────────────────────────────────┘
```

### Spectator View
```
┌─────────────────────────────────────────┐
│  🎯 Player1's Turn        ⏱️ 0:24       │
│  ━━━━━━━━━━━━━━━━░░░░░░                │
│                                          │
│  Which dessert matches your vibe?       │
│                                          │
│  🍰 Chocolate Cake                      │
│  🍨 Vanilla Ice Cream                   │
│  🥧 Apple Pie                           │
│  🍩 Donuts                              │
│                                          │
│  ┌────────────────────────────────┐    │
│  │ Quick Reactions:                │    │
│  │ 😂 Funny  🤔 Thinking  😱 Wow  │    │
│  │ 👍 Nice   👎 Nah      ⭐ Smart │    │
│  └────────────────────────────────┘    │
│                                          │
│  💡 Send Anonymous Suggestion (1 left) │
│  ┌────────────────────────────────┐    │
│  │ [Type suggestion...]            │    │
│  │                         [Send]  │    │
│  └────────────────────────────────┘    │
│                                          │
│  🔮 Predict: What will they pick?      │
│  [ ] Chocolate  [ ] Ice Cream           │
│  [ ] Apple Pie  [ ] Donuts              │
└─────────────────────────────────────────┘
```

### Team Dashboard (Team Challenge)
```
┌─────────────────────────────────────────┐
│  🔴 Red Team's Turn    ⏱️ 0:30 to vote │
│                                          │
│  What's your perfect weekend?           │
│                                          │
│  Team Vote:                              │
│  🏖️  Beach Day        ████░░ 2 votes   │
│  🎮 Gaming Marathon   ██░░░░ 1 vote    │
│  🍕 Food Tour         ░░░░░░ 0 votes   │
│  📚 Reading at Home   ██░░░░ 1 vote    │
│                                          │
│  Your Vote: [🎮 Gaming Marathon    ]   │
│                                          │
│  Team Chat:                              │
│  Player1: "Beach!"                       │
│  Player2: "No way, gaming!"              │
│                                          │
│  ⭐ Captain Player1 can lock in answer  │
│  [Lock in Team Answer]                   │
│                                          │
│  Scores: 🔴450 🔵380 🟢320 🟡290       │
└─────────────────────────────────────────┘
```

### Results Screen
```
┌─────────────────────────────────────────┐
│  🎉 Quiz Complete!                      │
│                                          │
│  ┌─────────────────────────────────┐   │
│  │  [AI Generated Result Image]     │   │
│  │                                   │   │
│  │  You're a Sunset Coffee! ☕🌅    │   │
│  └─────────────────────────────────┘   │
│                                          │
│  You're equal parts cozy and           │
│  adventurous, with a dash of mystery.  │
│  People are drawn to your warm energy  │
│  and spontaneous ideas!                 │
│                                          │
│  Your Answers:                           │
│  🍰 Rich & Indulgent (3x)               │
│  🎮 Gaming Marathon (2x)                │
│  🌙 Night Owl (1x)                      │
│                                          │
│  Stats:                                  │
│  ⚡ Speed: 94/100                        │
│  🎯 Crowd Favorite: 87%                 │
│  😂 Funniest Answer: Q4                │
│                                          │
│  [Share] [Next Player ➡️] [New Game]   │
└─────────────────────────────────────────┘
```

### Game Mode Selector
```
┌─────────────────────────────────────────┐
│  Choose Your Game Mode                   │
│                                          │
│  ┌───────────────────┐ ┌──────────────┐│
│  │ 🎤 Hot Seat       │ │ 👥 Team      ││
│  │                   │ │ Challenge    ││
│  │ One player at a   │ │              ││
│  │ time, all watch!  │ │ Compete in   ││
│  │                   │ │ teams!       ││
│  │ Players: 2-8      │ │ Players: 4-8 ││
│  │ Time: 5-10min     │ │ Time: 10-15m ││
│  │ [Select]          │ │ [Select]     ││
│  └───────────────────┘ └──────────────┘│
│  ┌───────────────────┐ ┌──────────────┐│
│  │ 🔮 Guess Who      │ │ ⚡ Speed     ││
│  │                   │ │ Round        ││
│  │ Who is this quiz  │ │              ││
│  │ really for?       │ │ Everyone     ││
│  │                   │ │ plays at     ││
│  │ Players: 3-8      │ │ once!        ││
│  │ Time: 10-15min    │ │ Players: 2-8 ││
│  │ [Select]          │ │ Time: 5min   ││
│  │ [Select]          │ │ [Select]     ││
│  └───────────────────┘ └──────────────┘│
│                                          │
│  More modes: Collaborative, Sabotage... │
└─────────────────────────────────────────┘
```

---

## 🏗️ Technical Architecture

### Frontend State Management
```
GameState {
  room: {
    id: string
    code: string
    hostId: string
    players: Player[]
    maxPlayers: number
    createdAt: timestamp
  }

  gameMode: {
    type: GameModeType
    config: GameModeConfig
    rules: Rules
    state: GameModeState
  }

  quiz: {
    topic: string
    questions: Question[]
    currentQuestion: number
    customization: QuizCustomization
  }

  teams?: {
    [teamId]: {
      name: string
      color: string
      players: string[]
      score: number
      captain: string
    }
  }

  turn: {
    activePlayerId: string
    phase: 'answering' | 'reacting' | 'results'
    timeRemaining: number
    responses: Response[]
  }

  interactions: {
    reactions: Reaction[]
    suggestions: Suggestion[]
    predictions: Prediction[]
    votes: Vote[]
  }

  scores: {
    [playerId]: {
      total: number
      breakdown: ScoreBreakdown
    }
  }
}
```

### Backend Models
```csharp
// Game Room Management
public class GameRoom {
  string RoomId
  string RoomCode
  string HostUserId
  GameMode Mode
  List<Player> Players
  RoomState State
  GameConfig Config
  DateTime CreatedAt
  DateTime? StartedAt
}

// Game Session
public class GameSession {
  string SessionId
  string RoomId
  GameMode Mode
  Quiz CurrentQuiz
  TurnState CurrentTurn
  Dictionary<string, int> Scores
  List<GameEvent> EventLog
}

// Turn Management
public class TurnState {
  string ActivePlayerId
  TurnPhase Phase
  int QuestionNumber
  DateTime StartTime
  int TimeLimit
  List<PlayerResponse> Responses
  List<Reaction> Reactions
}
```

---

## 🔄 Real-Time Communication

### Events (Discord SDK + WebSockets):
- `player_joined`
- `player_left`
- `game_started`
- `question_shown`
- `answer_submitted`
- `reaction_added`
- `suggestion_sent`
- `turn_changed`
- `phase_changed`
- `score_updated`
- `game_ended`

### Synchronization:
- Optimistic UI updates
- Server as source of truth
- Periodic state reconciliation
- Event replay on reconnect

---

## 🎯 Engagement Mechanics

### Rewards System:
- **Achievements**: First answer, Speed demon, Crowd pleaser
- **Titles**: Unlockable player titles based on play style
- **Stats**: Track lifetime stats, win rates, favorite modes
- **Leaderboards**: Room leaderboard, global stats

### Social Features:
- **Replay Moments**: Save and share funny moments
- **Highlight Reel**: Auto-generated best moments
- **Group Photo**: AI-generated group personality image
- **Discord Rich Presence**: Show what mode you're playing

---

## 📊 Modular Game Mode Framework

```typescript
interface GameMode {
  id: string
  name: string
  description: string
  minPlayers: number
  maxPlayers: number
  estimatedTime: string

  // Lifecycle hooks
  onGameStart(room: GameRoom): void
  onTurnStart(turn: Turn): void
  onAnswer(response: Response): void
  onTurnEnd(turn: Turn): void
  onGameEnd(session: GameSession): Results

  // Rules
  canSubmitAnswer(player: Player): boolean
  calculateScore(response: Response): number
  getNextPlayer(room: GameRoom): Player

  // UI Components
  getLobbyComponent(): Component
  getActivePlayerComponent(): Component
  getSpectatorComponent(): Component
  getResultsComponent(): Component
}
```

### Adding New Game Modes:
1. Extend GameMode interface
2. Implement lifecycle hooks
3. Define scoring rules
4. Create UI components
5. Register in GameModeRegistry
6. Add to mode selector

---

## 🚀 Implementation Priority

**Phase 1: Core Foundation**
- [ ] Room management system
- [ ] Player join/leave handling
- [ ] Basic state synchronization
- [ ] Hot Seat mode (simplest)
- [ ] Lobby + Game Mode selector

**Phase 2: Interaction**
- [ ] Reaction system
- [ ] Suggestion system
- [ ] Real-time updates
- [ ] Timer system
- [ ] Turn management

**Phase 3: Team Features**
- [ ] Team formation
- [ ] Team Challenge mode
- [ ] Team voting
- [ ] Team chat

**Phase 4: Advanced Modes**
- [ ] Speed Round
- [ ] Guess the Player
- [ ] Collaborative Story
- [ ] Sabotage mode

**Phase 5: Polish**
- [ ] Animations & transitions
- [ ] Sound effects
- [ ] Achievement system
- [ ] Stats tracking
- [ ] Share functionality

---

## 🎨 Quiz Customization API

```csharp
public class QuizCustomization {
  // Topic
  TopicMode TopicMode { get; set; } // Random, Category, Custom
  string? Category { get; set; } // Food, Movies, Gaming, etc.
  string? CustomPrompt { get; set; }

  // Style
  QuestionStyle Style { get; set; } // Classic, Deep, Chaotic, Rapid, Story
  Difficulty Difficulty { get; set; } // Casual, Challenging, Absurd

  // Images
  bool IncludeImages { get; set; }
  ImageStyle? ImageStyle { get; set; } // Realistic, Cartoon, Anime, etc.
  ImageMood? ImageMood { get; set; } // Cheerful, Dark, Energetic, Calm

  // Results
  int PersonalityCount { get; set; } // 4, 6, 8 options
  ResultDepth ResultDepth { get; set; } // Quick, Detailed, Roast
  bool IncludeResultImage { get; set; }

  // Length
  int QuestionCount { get; set; } // 5-15
}
```

This design creates an **engaging, replayable, social experience** where every game feels unique thanks to AI generation and player interactions!
