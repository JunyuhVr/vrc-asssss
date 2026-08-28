
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// Chromix Trivia - 4-player buzz-in trivia game for VRChat.
/// Players join as P1-P4, then race to buzz in and answer multiple-choice questions.
/// Correct answer: +10 pts. Wrong answer: other players get a chance.
/// After 15 rounds, highest score wins.
/// Cyberpunk neon theme: P1=cyan, P2=magenta, P3=green, P4=orange.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ChromixTriviaGame : UdonSharpBehaviour
{
    // --- Synced state ---
    [UdonSynced] private int _p1Score = 0;
    [UdonSynced] private int _p2Score = 0;
    [UdonSynced] private int _p3Score = 0;
    [UdonSynced] private int _p4Score = 0;
    [UdonSynced] private int _round = 0;
    [UdonSynced] private int _maxRounds = 15;
    [UdonSynced] private int _gameState = 0; // 0=idle, 1=question, 2=buzzed, 3=answering, 4=reveal, 5=gameover
    [UdonSynced] private int _buzzedPlayer = 0; // 0=none, 1-4
    [UdonSynced] private int _secondChance = 0; // 0=none, bitmask of players who can still buzz
    [UdonSynced] private int _currentQuestionIdx = 0;
    [UdonSynced] private int _p1Id = -1;
    [UdonSynced] private int _p2Id = -1;
    [UdonSynced] private int _p3Id = -1;
    [UdonSynced] private int _p4Id = -1;
    [UdonSynced] private string _p1Name = "";
    [UdonSynced] private string _p2Name = "";
    [UdonSynced] private string _p3Name = "";
    [UdonSynced] private string _p4Name = "";
    [UdonSynced] private int[] _questionOrder;
    [UdonSynced] private int _activePlayers = 0;
    [UdonSynced] private int _lastAnswerIdx = -1;
    [UdonSynced] private int _lastCorrect = -1;
    [UdonSynced] private int _revealTimer = 0;
    [UdonSynced] private int _joinRequestPlayerId = -1;
    [UdonSynced] private int _joinRequestSlot = 0;

    // --- UI references ---
    private Text _titleText;
    private Text _categoryText;
    private Text _roundText;
    private Text _statusText;
    private Text _questionText;
    private Text[] _answerTexts;
    private Button[] _answerButtons;
    private Image[] _answerImages;
    private Text[] _scoreTexts;
    private Text[] _nameTexts;
    private Image[] _panelImages;
    private Button[] _joinButtons;
    private Button[] _buzzButtons;
    private Button _startBtn;
    private Button _resetBtn;
    private Text _startBtnLabel;
    private Text _resetBtnLabel;

    // --- Embedded question pool (210 questions) ---
    private string[] _questions_text;
    private string[] _questions_a;
    private string[] _questions_b;
    private string[] _questions_c;
    private string[] _questions_d;
    private int[] _questions_correct;
    private string[] _questions_cat;
    private bool _questionsLoaded = false;

    // --- Colors ---
    private Color _cP1;
    private Color _cP2;
    private Color _cP3;
    private Color _cP4;
    private Color _cCorrect;
    private Color _cWrong;
    private Color _cNormal;
    private Color _cDim;
    private Color _cBG;

    // --- Local state ---
    private int _localPlayerNum = 0;
    private bool _uiReady = false;
    private int _pendingJoinSlot = 0; // >0 means we requested to join this slot, waiting for ownership
    private float _pendingJoinTimer = 0f;

    // --- Animation state ---
    private float _animTimer = 0f;
    private int _animPhase = 0; // 0=idle, 1=question entrance, 2=answer reveal, 3=score pulse, 4=gameover
    // _entranceStep removed - was unused
    private float _pulseTimer = 0f;
    // _pulseOn removed - was unused

    // --- Buzz cooldown ---
    private float _buzzCooldown = 0f;

    // --- Constants ---
    private const int MaxPlayers = 4;
    private const int AnswerCount = 4;

    private void Start()
    {
        LoadQuestions();
        _cP1 = new Color(0.157f, 0.847f, 0.961f, 1f);
        _cP2 = new Color(0.961f, 0.157f, 0.847f, 1f);
        _cP3 = new Color(0.165f, 0.878f, 0.533f, 1f);
        _cP4 = new Color(0.961f, 0.627f, 0.157f, 1f);
        _cCorrect = new Color(0.165f, 0.878f, 0.533f, 1f);
        _cWrong = new Color(0.961f, 0.220f, 0.220f, 1f);
        _cNormal = new Color(0.078f, 0.157f, 0.220f, 1f);
        _cDim = new Color(0.039f, 0.082f, 0.125f, 1f);
        _cBG = new Color(0.055f, 0.114f, 0.169f, 1f);

        FindUi();
        ApplyState(true);
        Debug.Log("[ChromixTrivia] Start() called. uiReady=" + _uiReady);
    }

    private void Update()
    {
        if (!_uiReady) return;

        // Retry pending join if ownership transfer was async
        if (_pendingJoinSlot > 0)
        {
            _pendingJoinTimer -= Time.deltaTime;
            if (_pendingJoinTimer <= 0f)
            {
                _pendingJoinSlot = 0;
            }
            else if (Networking.IsOwner(gameObject))
            {
                // We now own the object — process our own join request directly
                ProcessJoinRequest();
                _pendingJoinSlot = 0;
            }
        }

        // Handle reveal timer countdown
        if (_gameState == 4 && _revealTimer > 0)
        {
            _revealTimer--;
            if (_revealTimer <= 0 && Networking.IsOwner(gameObject))
            {
                NextRound();
            }
        }

        // Animations
        _animTimer += Time.deltaTime;
        _pulseTimer += Time.deltaTime;

        // Entrance animation - stagger answer buttons
        if (_animPhase == 1)
        {
            float staggerDelay = 0.08f;
            for (int i = 0; i < AnswerCount; i++)
            {
                if (_answerButtons[i] == null) continue;
                float targetTime = i * staggerDelay;
                if (_animTimer >= targetTime && _animTimer < targetTime + 0.3f)
                {
                    float t = (_animTimer - targetTime) / 0.3f;
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    RectTransform rt = _answerButtons[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        Vector2 basePos = GetAnswerBasePos(i);
                        rt.anchoredPosition = Vector2.Lerp(basePos + new Vector2(0f, -60f), basePos, eased);
                        CanvasRenderer cr = _answerButtons[i].GetComponent<CanvasRenderer>();
                        if (cr != null) cr.SetAlpha(eased);
                    }
                }
            }
            if (_animTimer > 0.5f) _animPhase = 0;
        }

        // Reveal animation - pulse correct answer
        if (_animPhase == 2)
        {
            float pulse = Mathf.Sin(_animTimer * 8f) * 0.5f + 0.5f;
            if (_lastCorrect >= 0 && _lastCorrect < AnswerCount && _answerImages[_lastCorrect] != null)
            {
                _answerImages[_lastCorrect].color = Color.Lerp(_cCorrect, Color.white, pulse * 0.4f);
            }
            if (_animTimer > 2f) _animPhase = 0;
        }

        // Score pulse animation
        if (_animPhase == 3)
        {
            float t = _animTimer / 0.5f;
            if (t < 1f)
            {
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
                for (int i = 0; i < MaxPlayers; i++)
                {
                    if (_scoreTexts[i] == null) continue;
                    _scoreTexts[i].rectTransform.localScale = Vector3.one * scale;
                }
            }
            else
            {
                for (int i = 0; i < MaxPlayers; i++)
                {
                    if (_scoreTexts[i] == null) continue;
                    _scoreTexts[i].rectTransform.localScale = Vector3.one;
                }
                _animPhase = 0;
            }
        }

        // Game over animation - alternating colors
        if (_animPhase == 4)
        {
            float t = Mathf.Sin(_animTimer * 3f) * 0.5f + 0.5f;
            if (_titleText != null)
            {
                _titleText.color = Color.Lerp(_cP1, _cP2, t);
            }
        }

        // Idle pulse on buzz buttons when question is active
        if (_gameState == 1 && _localPlayerNum > 0 && CanBuzz())
        {
            float pulse = Mathf.Sin(_pulseTimer * 4f) * 0.3f + 0.7f;
            int idx = _localPlayerNum - 1;
            if (_buzzButtons[idx] != null)
            {
                Image img = _buzzButtons[idx].GetComponent<Image>();
                if (img != null)
                {
                    Color c = PlayerColor(_localPlayerNum);
                    img.color = new Color(c.r * pulse, c.g * pulse, c.b * pulse, 1f);
                }
            }
        }

        // Buzz cooldown
        if (_buzzCooldown > 0f) _buzzCooldown -= Time.deltaTime;
    }

    private bool CanBuzz()
    {
        if (_gameState != 1) return false;
        if (_buzzedPlayer != 0) return false;
        if (_buzzCooldown > 0f) return false;
        // Check second chance bitmask
        int mask = 1 << (_localPlayerNum - 1);
        if (_secondChance != 0 && (_secondChance & mask) == 0) return false;
        return true;
    }

    private Vector2 GetAnswerBasePos(int i)
    {
        float y = 120f - i * 110f;
        return new Vector2(0f, y);
    }

    private Color PlayerColor(int num)
    {
        switch (num)
        {
            case 1: return _cP1;
            case 2: return _cP2;
            case 3: return _cP3;
            case 4: return _cP4;
            default: return Color.white;
        }
    }


    private void LoadQuestions()
    {
        if (_questionsLoaded) return;
                _questions_text = new string[] {
            "What is the capital of France?", "What is the largest planet in our solar system?",
            "Who painted the Mona Lisa?", "What is the chemical symbol for gold?",
            "In what year did World War II end?", "What is the tallest mountain in the world?",
            "Who wrote Romeo and Juliet?", "What is the fastest land animal?",
            "What is the currency of Japan?", "Who directed the movie Jaws?",
            "What is the largest ocean on Earth?", "What is the boiling point of water in Celsius?",
            "Who was the first president of the United States?",
            "What is the smallest country in the world?", "What band performed the song Bohemian Rhapsody?",
            "What is the main ingredient in guacamole?", "What does CPU stand for?",
            "How many continents are there on Earth?", "What is the capital of Australia?",
            "Who discovered penicillin?", "What is the largest mammal in the world?",
            "In what year did the Titanic sink?", "What is the national sport of Japan?",
            "Who wrote the Harry Potter series?", "What is the hardest natural substance on Earth?",
            "What is the capital of Canada?", "What gas do plants absorb from the atmosphere?",
            "Who painted The Starry Night?", "What is the square root of 144?",
            "What is the largest desert in the world?", "Who invented the telephone?",
            "What is the capital of Brazil?", "What is the most spoken language in the world?",
            "What movie features the character Jack Dawson?", "What is the largest bone in the human body?",
            "What is the capital of Egypt?", "How many sides does a hexagon have?",
            "What is the freezing point of water in Fahrenheit?", "Who wrote The Great Gatsby?",
            "What is the fastest bird in the world?", "What is the capital of Russia?",
            "What is the chemical symbol for silver?", "Who was the first man on the moon?",
            "What is the largest country by area?", "What instrument did Louis Armstrong play?",
            "What is the capital of Italy?", "What is the powerhouse of the cell?",
            "Who painted the Sistine Chapel ceiling?", "What is the capital of South Korea?",
            "How many players are on a soccer team?", "What is the most abundant gas in Earth's atmosphere?",
            "Who wrote 1984?", "What is the capital of India?", "What is the largest island in the world?",
            "What year did the Berlin Wall fall?", "What is the capital of Mexico?",
            "What is the speed of light approximately?", "Who directed the movie Inception?",
            "What is the national flower of Japan?", "What is the capital of Spain?",
            "What is the smallest planet in our solar system?", "Who wrote To Kill a Mockingbird?",
            "What is the capital of Argentina?", "What is the largest reef system in the world?",
            "What is the chemical symbol for iron?", "Who was the first female PM of the United Kingdom?",
            "What is the capital of Norway?", "How many strings does a standard guitar have?",
            "What is the capital of Greece?", "What is the largest volcano on Earth?",
            "Who composed the Four Seasons?", "What is the capital of Thailand?",
            "What does HTML stand for?", "What is the capital of Turkey?",
            "How many bones are in the adult human body?", "Who painted The Persistence of Memory?",
            "What is the capital of Sweden?", "What is the national animal of the USA?",
            "Who wrote The Odyssey?", "What is the capital of Portugal?",
            "What is the largest moon in the solar system?", "What is the capital of South Africa?",
            "What sport uses a racket and a shuttlecock?", "Who invented the light bulb?",
            "What is the capital of Poland?", "What is the chemical symbol for sodium?",
            "What is the capital of Ireland?", "How many degrees in a triangle?",
            "Who directed the movie Pulp Fiction?", "What is the capital of Vietnam?",
            "What is the largest cat species?", "What is the capital of Iran?",
            "What is the rarest blood type?", "Who wrote Pride and Prejudice?",
            "What is the capital of Kenya?", "What is the deepest ocean trench?",
            "What is the capital of Belgium?", "How many planets are in our solar system?",
            "Who was known as the Queen of Soul?", "What is the capital of Finland?",
            "What is the largest artery in the human body?", "What is the capital of Austria?",
            "What is the chemical symbol for lead?", "Who wrote The Catcher in the Rye?",
            "What is the capital of Denmark?", "What is the national sport of England?",
            "What is the smallest unit of life?", "Who painted The Last Supper?",
            "What is the capital of Hungary?", "What is the largest country in South America?",
            "Who composed Symphony No. 9?", "What is the capital of Malaysia?",
            "What is the chemical symbol for mercury?", "Who directed the movie Avatar?",
            "What is the capital of Indonesia?", "What is the fastest fish in the ocean?",
            "What is the capital of Chile?", "How many time zones does Russia have?",
            "Who wrote War and Peace?", "What is the capital of Peru?",
            "What is the largest glacier in the world?", "What is the capital of Colombia?",
            "What does NASA stand for?", "Who was the first emperor of Rome?",
            "What is the capital of New Zealand?", "What is the heaviest organ in the human body?",
            "Who painted Girl with a Pearl Earring?", "What is the capital of Ukraine?",
            "What is the largest stadium in the world?", "Who wrote Frankenstein?",
            "What is the capital of Switzerland?", "What is the chemical symbol for tin?",
            "Who directed the movie The Godfather?", "What is the capital of Morocco?",
            "What is the national animal of Australia?", "How many chambers does the human heart have?",
            "Who wrote The Hobbit?", "What is the capital of Romania?",
            "What is the largest peninsula in the world?", "What is the capital of Czech Republic?",
            "What is the chemical symbol for copper?", "Who painted The Birth of Venus?",
            "What is the capital of Philippines?", "What is the longest river in the world?",
            "Who composed Fur Elise?", "What is the capital of Bangladesh?",
            "What is the most popular social media platform?", "Who directed the movie Titanic?",
            "What is the capital of Saudi Arabia?", "What is the largest butterfly in the world?",
            "What is the capital of Iraq?", "What is the chemical symbol for potassium?",
            "Who wrote The Lord of the Rings?", "What is the capital of Cuba?",
            "What is the smallest dog breed?", "Who was the first woman in space?",
            "What is the capital of Algeria?", "What is the largest lake in the world?",
            "Who painted American Gothic?", "What is the capital of Netherlands?",
            "What is the chemical symbol for helium?", "Who directed the movie The Matrix?",
            "What is the capital of Singapore?", "What is the fastest swimming stroke?",
            "Who wrote Moby Dick?", "What is the capital of Lebanon?",
            "What is the largest asteroid in the solar system?", "Who composed The Magic Flute?",
            "What is the capital of Jordan?", "What is the chemical symbol for calcium?",
            "Who directed the movie Forrest Gump?", "What is the capital of Kuwait?",
            "What is the national sport of China?", "Who wrote Brave New World?",
            "What is the capital of Ethiopia?", "What is the largest species of bear?",
            "Who painted The Kiss?", "What is the capital of Venezuela?",
            "What is the chemical symbol for zinc?", "Who directed the movie Gladiator?",
            "What is the capital of Myanmar?", "What is the largest spider in the world?",
            "Who wrote The Adventures of Huckleberry Finn?", "What is the capital of Ecuador?",
            "What is the hottest planet in the solar system?", "Who composed the Brandenburg Concertos?",
            "What is the capital of Libya?", "What is the chemical symbol for platinum?",
            "Who directed the movie Jurassic Park?", "What is the capital of Sudan?",
            "What is the largest rodent in the world?", "Who painted The Garden of Earthly Delights?",
            "What is the capital of Yemen?", "What is the chemical symbol for uranium?",
            "Who wrote Crime and Punishment?", "What is the capital of Tunisia?",
            "What is the deepest lake in the world?", "Who directed The Shawshank Redemption?",
            "What is the capital of Oman?", "What is the national sport of Canada?",
            "Who painted Las Meninas?", "What is the capital of Panama?",
            "What is the chemical symbol for neon?", "Who wrote Wuthering Heights?",
            "What is the capital of Uruguay?", "What is the largest species of shark?",
            "Who composed the William Tell Overture?", "What is the capital of Paraguay?",
            "What is the chemical symbol for chlorine?", "Who directed the movie Goodfellas?"
        };        _questions_a = new string[] {
            "Berlin", "Neptune", "Pablo Picasso", "Gd", "1944", "K2", "Jane Austen", "Lion", "Yen",
            "Martin Scorsese", "Indian Ocean", "120 degrees", "Abraham Lincoln", "Nauru", "Queen", "Lime",
            "Computer Process Unit", "5", "Perth", "Alexander Fleming", "Blue whale", "1912", "Karate",
            "Roald Dahl", "Quartz", "Toronto", "Hydrogen", "Pablo Picasso", "13", "Antarctic Desert",
            "Guglielmo Marconi", "Brasilia", "Mandarin Chinese", "Avatar", "Tibia", "Alexandria", "5",
            "32 degrees", "Ernest Hemingway", "White-throated needletail", "Novosibirsk", "Sl",
            "Michael Collins", "United States", "Saxophone", "Naples", "Chloroplast", "Raphael", "Pyongyang",
            "10", "Argon", "Aldous Huxley", "New Delhi", "Greenland", "1987", "Mexico City", "300000 km/s",
            "Steven Spielberg", "Chrysanthemum", "Bilbao", "Mars", "John Steinbeck", "Lima",
            "Great Barrier Reef", "Ir", "Winston Churchill", "Bergen", "8", "Athens", "Mount Fuji",
            "George Frideric Handel", "Seoul", "HyperText Markup Level", "Izmir", "208", "Edvard Munch",
            "Gothenburg", "Grizzly bear", "Homer", "Faro", "Callisto", "Pretoria", "Badminton",
            "Benjamin Franklin", "Wroclaw", "K", "Cork", "360", "Christopher Nolan", "Da Nang", "Cheetah",
            "Tabriz", "A negative", "Jane Austen", "Nairobi", "Java Trench", "Ghent", "9", "Gladys Knight",
            "Oulu", "Carotid artery", "Salzburg", "Ld", "F. Scott Fitzgerald", "Aarhus", "Tennis",
            "Organelle", "Caravaggio", "Miskolc", "Argentina", "Pyotr Tchaikovsky", "Kuala Lumpur", "Mc",
            "Steven Spielberg", "Surabaya", "Marlin", "Vina del Mar", "9", "Vladimir Nabokov", "Trujillo",
            "Petermann Glacier", "Bogota", "National Air and Space Administration", "Julius Caesar",
            "Hamilton", "Brain", "Anthony van Dyck", "Odesa", "Rungrado May Day Stadium", "Shelley", "Bern",
            "Ti", "Francis Ford Coppola", "Marrakech", "Kangaroo", "5", "C.S. Lewis", "Iasi",
            "Indian Peninsula", "Plzen", "Cu", "Caravaggio", "Manila", "Nile River", "Johann Sebastian Bach",
            "Chittagong", "TikTok", "Ridley Scott", "Riyadh", "Queen Alexandra's birdwing", "Baghdad", "Pt",
            "J.R.R. Tolkien", "Camaguey", "Pug", "Sally Ride", "Oran", "Lake Victoria", "Grant Wood",
            "Eindhoven", "Ho", "Lana and Lilly Wachowski", "Jurong", "Backstroke", "Herman Melville",
            "Beirut", "Pallas", "Richard Wagner", "Amman", "Ca", "Ridley Scott", "Kuwait City", "Badminton",
            "Ray Bradbury", "Dire Dawa", "Brown bear", "Gustav Klimt", "Maracaibo", "Zi",
            "Christopher Nolan", "Naypyidaw", "Tarantula", "Bret Harte", "Cuenca", "Venus",
            "George Frideric Handel", "Misrata", "Pa", "Joe Johnston", "Wad Madani", "Beaver",
            "Hieronymus Bosch", "Sanaa", "Un", "Leo Tolstoy", "Sfax", "Lake Tanganyika", "Steven Spielberg",
            "Sohar", "Hockey", "Diego Velazquez", "Colon", "Nn", "Jane Austen", "Asuncion",
            "Great white shark", "Giacomo Meyerbeer", "Ciudad del Este", "Cl", "Robert Zemeckis"
        };        _questions_b = new string[] {
            "London", "Earth", "Raphael", "Go", "1946", "Lhotse", "Charles Dickens", "Greyhound", "Yuan",
            "Ridley Scott", "Atlantic Ocean", "110 degrees", "Thomas Jefferson", "Tuvalu", "The Beatles",
            "Avocado", "Central Processing Unity", "8", "Melbourne", "Marie Curie", "Giraffe", "1914",
            "Judo", "George R.R. Martin", "Diamond", "Montreal", "Nitrogen", "Edvard Munch", "14",
            "Sahara Desert", "Alexander Graham Bell", "Salvador", "English", "The Aviator", "Humerus",
            "Luxor", "6", "212 degrees", "F. Scott Fitzgerald", "Golden eagle", "Saint Petersburg", "Si",
            "Buzz Aldrin", "Russia", "Trumpet", "Rome", "Nucleus", "Leonardo da Vinci", "Seoul", "9",
            "Oxygen", "H.G. Wells", "Chennai", "Borneo", "1989", "Acapulco", "150000 km/s",
            "Martin Scorsese", "Sunflower", "Madrid", "Venus", "J.D. Salinger", "Rosario", "Coral Sea Reef",
            "Fe", "Theresa May", "Trondheim", "7", "Corinth", "Mauna Loa", "Johann Sebastian Bach", "Phuket",
            "HyperText Transfer Protocol", "Ankara", "206", "Rene Magritte", "Uppsala", "Bison", "Ovid",
            "Lisbon", "Ganymede", "Bloemfontein", "Racquetball", "Humphry Davy", "Lodz", "Sd", "Dublin",
            "270", "Robert Zemeckis", "Hanoi", "Siberian tiger", "Shiraz", "O negative", "Emily Bronte",
            "Nakuru", "Mariana Trench", "Liege", "10", "Tina Turner", "Turku", "Pulmonary artery", "Vienna",
            "Pb", "J.D. Salinger", "Aalborg", "Cricket", "Atom", "Leonardo da Vinci", "Debrecen", "Chile",
            "Wolfgang Amadeus Mozart", "George Town", "Hg", "Christopher Nolan", "Medan", "Swordfish",
            "Santiago", "12", "Anton Chekhov", "Arequipa", "Lambert Glacier", "Bucaramanga",
            "National Aeronautics and Space Admin", "Caligula", "Auckland", "Skin", "Johannes Vermeer",
            "Kharkiv", "Wembley Stadium", "Mary Wollstonecraft", "Geneva", "Tn", "Ridley Scott",
            "Casablanca", "Emu", "4", "J.R.R. Tolkien", "Cluj-Napoca", "Scandinavian Peninsula", "Prague",
            "Co", "Leonardo da Vinci", "Cebu", "Mississippi River", "Franz Schubert", "Dhaka", "Facebook",
            "James Cameron", "Jeddah", "Swallowtail butterfly", "Erbil", "K", "George R.R. Martin",
            "Santiago de Cuba", "Beagle", "Christa McAuliffe", "Algiers", "Caspian Sea", "Andrew Wyeth",
            "The Hague", "Ha", "Christopher Nolan", "Tampines", "Breaststroke", "Nathaniel Hawthorne",
            "Tripoli", "Vesta", "Wolfgang Amadeus Mozart", "Aqaba", "Cd", "Steven Spielberg", "Hawalli",
            "Table tennis", "Isaac Asimov", "Addis Ababa", "Panda", "Egon Schiele", "Valencia", "Zn",
            "Ridley Scott", "Bago", "Goliath birdeater", "Jack London", "Quito", "Earth",
            "Johann Sebastian Bach", "Sabha", "Pl", "Steven Spielberg", "Omdurman", "Nutria", "Titian",
            "Taiz", "Um", "Ivan Turgenev", "Sousse", "Lake Superior", "Robert Zemeckis", "Nizwa",
            "Basketball", "Francisco Goya", "Bocas del Toro", "Ne", "Anne Bronte", "Montevideo",
            "Hammerhead shark", "Giuseppe Verdi", "Pilar", "Ch", "Steven Spielberg"
        };        _questions_c = new string[] {
            "Madrid", "Jupiter", "Michelangelo", "Au", "1943", "Kangchenjunga", "William Shakespeare",
            "Cheetah", "Won", "Steven Spielberg", "Arctic Ocean", "90 degrees", "George Washington",
            "Monaco", "The Rolling Stones", "Onion", "Central Process Unit", "7", "Sydney", "Louis Pasteur",
            "Hippopotamus", "1913", "Baseball", "J.K. Rowling", "Gold", "Ottawa", "Oxygen",
            "Vincent van Gogh", "12", "Gobi Desert", "Elisha Gray", "Sao Paulo", "Hindi", "Titanic", "Femur",
            "Giza", "7", "0 degrees", "Tennessee Williams", "Spur-winged goose", "Moscow", "Ag",
            "Yuri Gagarin", "China", "Clarinet", "Florence", "Ribosome", "Michelangelo", "Beijing", "12",
            "Carbon dioxide", "Ray Bradbury", "Kolkata", "New Guinea", "1990", "Guadalajara", "299792 km/s",
            "Robert Zemeckis", "Lotus", "Seville", "Pluto", "Harper Lee", "Buenos Aires",
            "Belize Barrier Reef", "Iron", "Margaret Thatcher", "Oslo", "5", "Rome", "Mount Etna",
            "Henry Purcell", "Bangkok", "HyperText Markup Language", "Bursa", "201", "Pablo Picasso",
            "Malmo", "Turkey", "Virgil", "Setubal", "Io", "Cape Town", "Tennis", "Thomas Edison", "Warsaw",
            "Na", "Galway", "90", "Quentin Tarantino", "Hai Phong", "Jaguar", "Dubai", "AB negative",
            "Bram Stoker", "Kisumu", "Puerto Rico Trench", "Antwerp", "7", "Aretha Franklin", "Espoo",
            "Aorta", "Linz", "La", "Truman Capote", "Odense", "Football", "Cell", "Pablo Picasso", "Szeged",
            "Brazil", "Ludwig van Beethoven", "Ipoh", "Mr", "Ridley Scott", "Bali", "Sailfish", "Valparaiso",
            "11", "Fyodor Dostoevsky", "Lima", "Beardmore Glacier", "Cartagena",
            "North American Space Agency", "Augustus", "Wellington", "Liver", "Rembrandt", "Kyiv",
            "Camp Nou", "Bram Stoker", "Zurich", "St", "Martin Scorsese", "Fes", "Wombat", "6",
            "J.K. Rowling", "Bucharest", "Iberian Peninsula", "Brno", "Ce", "Raphael", "Quezon City",
            "Amazon River", "Ludwig van Beethoven", "Sylhet", "YouTube", "Steven Spielberg", "Mecca",
            "Monarch butterfly", "Basra", "Po", "C.S. Lewis", "Holguin", "Chihuahua", "Svetlana Savitskaya",
            "Constantine", "Lake Superior", "Edward Hopper", "Amsterdam", "Hl", "James Cameron", "Woodlands",
            "Butterfly", "Jack London", "Sidon", "Ceres", "Giuseppe Verdi", "Zarqa", "Cm",
            "Christopher Nolan", "Farwaniya", "Soccer", "Aldous Huxley", "Gondar", "Polar bear",
            "Edvard Munch", "Caracas", "Zc", "Steven Spielberg", "Yangon", "Huntsman spider", "Mark Twain",
            "Guayaquil", "Mercury", "Franz Joseph Haydn", "Benghazi", "Pm", "Christopher Nolan",
            "Port Sudan", "Capybara", "El Greco", "Aden", "Ur", "Anton Chekhov", "Kairouan", "Lake Vostok",
            "Christopher Nolan", "Muscat", "Curling", "Bartolome Murillo", "Panama City", "Nm",
            "Charlotte Bronte", "Punta del Este", "Tiger shark", "Richard Wagner", "Encarnacion", "Ci",
            "Martin Scorsese"
        };        _questions_d = new string[] {
            "Paris", "Saturn", "Leonardo da Vinci", "Ag", "1945", "Mount Everest", "Ernest Hemingway",
            "Pronghorn", "Rupee", "George Lucas", "Pacific Ocean", "100 degrees", "John Adams",
            "Vatican City", "Led Zeppelin", "Tomato", "Central Processing Unit", "6", "Canberra",
            "Isaac Newton", "African elephant", "1911", "Sumo wrestling", "Stephen King", "Topaz",
            "Vancouver", "Carbon dioxide", "Claude Monet", "10", "Karakum Desert", "Thomas Edison",
            "Rio de Janeiro", "Spanish", "The Notebook", "Pelvis", "Cairo", "8", "100 degrees",
            "John Steinbeck", "Peregrine falcon", "Kazan", "Sv", "Neil Armstrong", "Canada", "Piano",
            "Milan", "Mitochondria", "Donatello", "Tokyo", "11", "Nitrogen", "George Orwell", "Mumbai",
            "Madagascar", "1991", "Monterrey", "200000 km/s", "Christopher Nolan", "Cherry blossom",
            "Barcelona", "Mercury", "Mark Twain", "Cordoba", "Great Astrolabe Reef", "Ferrum", "John Major",
            "Stavanger", "6", "Sparta", "Mauna Kea", "Antonio Vivaldi", "Chiang Mai",
            "HyperText Transfer Markup Language", "Istanbul", "207", "Salvador Dali", "Stockholm",
            "Bald eagle", "Sophocles", "Porto", "Titan", "Johannesburg", "Squash", "Nikola Tesla", "Krakow",
            "So", "Limerick", "180", "Martin Scorsese", "Ho Chi Minh City", "Bengal tiger", "Tehran",
            "B negative", "Charlotte Bronte", "Mombasa", "Tonga Trench", "Brussels", "8", "Diana Ross",
            "Helsinki", "Vena cava", "Graz", "Le", "John Steinbeck", "Copenhagen", "Rugby", "Molecule",
            "Titian", "Budapest", "Colombia", "Johannes Brahms", "Singapore", "Me", "James Cameron",
            "Jakarta", "Black marlin", "Concepcion", "10", "Leo Tolstoy", "Cusco", "Antarctic Ice Sheet",
            "Medellin", "National Aviation and Space Agency", "Nero", "Christchurch", "Heart", "Frans Hals",
            "Lviv", "Narendra Modi Stadium", "Mary Shelley", "Lucerne", "Sn", "Steven Spielberg", "Rabat",
            "Koala", "7", "George Orwell", "Timisoara", "Arabian Peninsula", "Ostrava", "Cp",
            "Sandro Botticelli", "Davao", "Yangtze River", "Frederic Chopin", "Khulna", "Instagram",
            "Christopher Nolan", "Dammam", "Blue morpho butterfly", "Mosul", "Ka", "Terry Brooks", "Havana",
            "Pomeranian", "Valentina Tereshkova", "Annaba", "Lake Michigan", "Norman Rockwell", "Rotterdam",
            "He", "Steven Spielberg", "Singapore", "Freestyle", "Robert Louis Stevenson", "Tyre", "Hygiea",
            "Johann Sebastian Bach", "Irbid", "Cl", "Robert Zemeckis", "Al Jahra", "Martial arts",
            "George Orwell", "Mekele", "Grizzly bear", "Oskar Kokoschka", "Barquisimeto", "Zk",
            "Robert Zemeckis", "Mandalay", "Bird-eating spider", "Robert Louis Stevenson", "Ambato", "Mars",
            "Antonio Vivaldi", "Tripoli", "Pt", "Michael Crichton", "Khartoum", "Coypu",
            "Pieter Bruegel the Elder", "Al Hudaydah", "U", "Fyodor Dostoevsky", "Tunis", "Lake Baikal",
            "Frank Darabont", "Salalah", "Lacrosse", "El Greco", "David", "No", "Emily Bronte",
            "Colonia del Sacramento", "Whale shark", "Gioachino Rossini", "Asuncion", "Ce",
            "Christopher Nolan"
        };        _questions_correct = new int[] {
            3, 2, 3, 2, 3, 3, 2, 2, 0, 2, 3, 3, 2, 3, 0, 1, 3, 2, 3, 0, 0, 0, 3, 2, 1, 2, 3, 2, 2, 0, 1, 0,
            0, 2, 2, 3, 1, 0, 1, 3, 2, 2, 3, 1, 1, 1, 3, 2, 1, 3, 3, 3, 0, 0, 1, 0, 0, 3, 3, 1, 3, 2, 2, 0,
            1, 2, 2, 3, 0, 1, 3, 2, 2, 1, 1, 3, 3, 3, 0, 1, 1, 0, 0, 2, 2, 2, 1, 3, 2, 1, 1, 3, 2, 0, 0, 1,
            3, 3, 2, 3, 2, 1, 1, 1, 3, 1, 2, 1, 3, 2, 2, 0, 1, 3, 3, 2, 1, 2, 3, 2, 1, 0, 1, 2, 2, 2, 1, 2,
            0, 3, 0, 3, 0, 3, 0, 1, 1, 2, 3, 1, 0, 3, 0, 0, 2, 1, 1, 1, 0, 0, 0, 1, 0, 3, 2, 3, 1, 1, 0, 2,
            3, 0, 3, 3, 0, 0, 2, 1, 0, 0, 3, 0, 1, 2, 1, 2, 0, 2, 1, 1, 0, 1, 2, 1, 0, 1, 3, 3, 1, 3, 2, 0,
            0, 3, 3, 3, 3, 3, 2, 3, 0, 2, 1, 3, 1, 3, 3, 3, 0, 2
        };        _questions_cat = new string[] {
            "Geography", "Space", "Art", "Science", "History", "Geography", "Literature", "Animals",
            "Geography", "Movies", "Geography", "Science", "History", "Geography", "Music", "Food",
            "Technology", "Geography", "Geography", "Science", "Animals", "History", "Sports", "Literature",
            "Science", "Geography", "Nature", "Art", "Math", "Geography", "History", "Geography",
            "Pop Culture", "Movies", "Science", "Geography", "Math", "Science", "Literature", "Animals",
            "Geography", "Science", "History", "Geography", "Music", "Geography", "Science", "Art",
            "Geography", "Sports", "Science", "Literature", "Geography", "Geography", "History", "Geography",
            "Science", "Movies", "Nature", "Geography", "Space", "Literature", "Geography", "Nature",
            "Science", "History", "Geography", "Music", "Geography", "Nature", "Music", "Geography",
            "Technology", "Geography", "Science", "Art", "Geography", "Animals", "Literature", "Geography",
            "Space", "Geography", "Sports", "History", "Geography", "Science", "Geography", "Math", "Movies",
            "Geography", "Animals", "Geography", "Science", "Literature", "Geography", "Nature", "Geography",
            "Space", "Music", "Geography", "Science", "Geography", "Science", "Literature", "Geography",
            "Sports", "Science", "Art", "Geography", "Geography", "Music", "Geography", "Science", "Movies",
            "Geography", "Animals", "Geography", "Geography", "Literature", "Geography", "Nature",
            "Geography", "Technology", "History", "Geography", "Science", "Art", "Geography", "Sports",
            "Literature", "Geography", "Science", "Movies", "Geography", "Animals", "Science", "Literature",
            "Geography", "Geography", "Geography", "Science", "Art", "Geography", "Nature", "Music",
            "Geography", "Technology", "Movies", "Geography", "Animals", "Geography", "Science",
            "Literature", "Geography", "Animals", "History", "Geography", "Nature", "Art", "Geography",
            "Science", "Movies", "Geography", "Sports", "Literature", "Geography", "Space", "Music",
            "Geography", "Science", "Movies", "Geography", "Sports", "Literature", "Geography", "Animals",
            "Art", "Geography", "Science", "Movies", "Geography", "Animals", "Literature", "Geography",
            "Space", "Music", "Geography", "Science", "Movies", "Geography", "Animals", "Art", "Geography",
            "Science", "Literature", "Geography", "Nature", "Movies", "Geography", "Sports", "Art",
            "Geography", "Science", "Literature", "Geography", "Animals", "Music", "Geography", "Science",
            "Movies"
        };
        _questionsLoaded = true;
    }

    // --- UI Discovery ---
    private void FindUi()
    {
        Transform asset = transform.parent;
        if (asset == null) { Debug.LogError("[ChromixTrivia] No parent found."); return; }
        Transform canvas = asset.Find("UI_Canvas");
        if (canvas == null) { Debug.LogError("[ChromixTrivia] UI_Canvas not found."); return; }

        _titleText = FindText(canvas, "Frame/Header/Title");
        _categoryText = FindText(canvas, "Frame/Header/CategoryText");
        _roundText = FindText(canvas, "Frame/Header/RoundText");
        _statusText = FindText(canvas, "Frame/StatusCard/StatusText");
        _questionText = FindText(canvas, "Frame/QuestionPanel/QuestionText");

        _answerTexts = new Text[AnswerCount];
        _answerButtons = new Button[AnswerCount];
        _answerImages = new Image[AnswerCount];
        for (int i = 0; i < AnswerCount; i++)
        {
            Transform ans = canvas.Find("Frame/AnswerPanel/Answer_" + i);
            if (ans != null)
            {
                _answerButtons[i] = ans.GetComponent<Button>();
                _answerImages[i] = ans.GetComponent<Image>();
                Transform label = ans.Find("Label");
                if (label != null) _answerTexts[i] = label.GetComponent<Text>();
            }
        }

        _scoreTexts = new Text[MaxPlayers];
        _nameTexts = new Text[MaxPlayers];
        _panelImages = new Image[MaxPlayers];
        _joinButtons = new Button[MaxPlayers];
        _buzzButtons = new Button[MaxPlayers];

        for (int i = 0; i < MaxPlayers; i++)
        {
            int p = i + 1;
            Transform panel = canvas.Find("Frame/P" + p + "Panel");
            if (panel != null)
            {
                _panelImages[i] = panel.GetComponent<Image>();
                _scoreTexts[i] = FindText(panel, "ScoreText");
                _nameTexts[i] = FindText(panel, "NameText");
                Transform join = panel.Find("JoinBtn");
                if (join != null) _joinButtons[i] = join.GetComponent<Button>();
                Transform buzz = panel.Find("BuzzBtn");
                if (buzz != null) _buzzButtons[i] = buzz.GetComponent<Button>();
            }
        }

        Transform startT = canvas.Find("Frame/Controls/StartBtn");
        if (startT != null)
        {
            _startBtn = startT.GetComponent<Button>();
            _startBtnLabel = FindText(startT, "Label");
        }
        Transform resetT = canvas.Find("Frame/Controls/ResetBtn");
        if (resetT != null)
        {
            _resetBtn = resetT.GetComponent<Button>();
            _resetBtnLabel = FindText(resetT, "Label");
        }

        _uiReady = (_questionText != null && _answerButtons[0] != null);
        Debug.Log("[ChromixTrivia] FindUi done. uiReady=" + _uiReady + " questionText=" + (_questionText != null));
    }

    private Text FindText(Transform parent, string path)
    {
        Transform t = parent.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    // --- State Application ---
    private void ApplyState(bool force)
    {
        if (!_uiReady) return;

        // Title
        if (_titleText != null)
        {
            _titleText.text = "CHROMIX TRIVIA";
            _titleText.color = _cP1;
        }

        // Round
        if (_roundText != null)
        {
            if (_gameState == 0) _roundText.text = "ROUND 0 / " + _maxRounds;
            else if (_gameState == 5) _roundText.text = "FINAL RESULTS";
            else _roundText.text = "ROUND " + _round + " / " + _maxRounds;
        }

        // Status
        if (_statusText != null)
        {
            switch (_gameState)
            {
                case 0: _statusText.text = _activePlayers > 0 ? "PRESS START TO BEGIN" : "JOIN TO PLAY!"; break;
                case 1: _statusText.text = "BUZZ IN TO ANSWER!"; break;
                case 2: _statusText.text = GetPlayerName(_buzzedPlayer) + " BUZZED IN!"; break;
                case 3: _statusText.text = GetPlayerName(_buzzedPlayer) + " IS ANSWERING..."; break;
                case 4:
                    if (_lastCorrect == _lastAnswerIdx) _statusText.text = "CORRECT! +10 PTS";
                    else _statusText.text = "WRONG ANSWER!";
                    break;
                case 5: _statusText.text = "GAME OVER! " + GetWinnerName() + " WINS!"; break;
            }
        }

        // Player panels
        for (int i = 0; i < MaxPlayers; i++)
        {
            int p = i + 1;
            bool joined = IsPlayerJoined(p);
            Color pc = PlayerColor(p);

            if (_panelImages[i] != null)
            {
                _panelImages[i].color = joined ? new Color(pc.r * 0.15f, pc.g * 0.15f, pc.b * 0.15f, 0.9f) : _cDim;
            }
            if (_nameTexts[i] != null)
            {
                _nameTexts[i].text = joined ? GetPlayerName(p) : "OPEN SLOT";
                _nameTexts[i].color = joined ? pc : _cDim;
            }
            if (_scoreTexts[i] != null)
            {
                _scoreTexts[i].text = joined ? GetScore(p).ToString() : "--";
                _scoreTexts[i].color = joined ? pc : _cDim;
            }
            if (_joinButtons[i] != null)
            {
                _joinButtons[i].gameObject.SetActive(_gameState == 0 && !joined);
            }
            if (_buzzButtons[i] != null)
            {
                bool canBuzz = _gameState == 1 && joined && _localPlayerNum == p && CanBuzz();
                _buzzButtons[i].gameObject.SetActive(canBuzz);
            }
        }

        // Question and answers
        if (_gameState == 1 || _gameState == 2 || _gameState == 3 || _gameState == 4)
        {
            ShowQuestion();
        }
        else
        {
            HideQuestion();
        }

        // Start/Reset buttons
        if (_startBtn != null)
        {
            _startBtn.gameObject.SetActive(_gameState == 0 && _activePlayers > 0);
            if (_startBtnLabel != null) _startBtnLabel.text = "START GAME";
        }
        if (_resetBtn != null)
        {
            _resetBtn.gameObject.SetActive(_gameState == 5 || _gameState == 0);
            if (_resetBtnLabel != null) _resetBtnLabel.text = _gameState == 5 ? "NEW GAME" : "RESET";
        }

        // Game over animation
        if (_gameState == 5)
        {
            _animPhase = 4;
            _animTimer = 0f;
        }
    }

    private void ShowQuestion()
    {
        if (_questionText == null) return;
        if (_questionOrder == null || _currentQuestionIdx < 0 || _currentQuestionIdx >= _questions_text.Length)
        {
            _questionText.text = "Loading...";
            return;
        }

        int qIdx = _questionOrder[_currentQuestionIdx];
        _questionText.text = _questions_text[qIdx];
        if (_categoryText != null) _categoryText.text = _questions_cat[qIdx];

        for (int i = 0; i < AnswerCount; i++)
        {
            if (_answerButtons[i] == null) continue;
            _answerButtons[i].gameObject.SetActive(true);

            string answer = "";
            switch (i)
            {
                case 0: answer = _questions_a[qIdx]; break;
                case 1: answer = _questions_b[qIdx]; break;
                case 2: answer = _questions_c[qIdx]; break;
                case 3: answer = _questions_d[qIdx]; break;
            }
            if (_answerTexts[i] != null) _answerTexts[i].text = answer;

            // Answer button states
            if (_gameState == 1)
            {
                // Question active - dim answers, waiting for buzz
                _answerButtons[i].interactable = false;
                if (_answerImages[i] != null) _answerImages[i].color = _cNormal;
            }
            else if (_gameState == 2 || _gameState == 3)
            {
                // Buzzed - show answers, enable for buzzed player
                bool isMyTurn = _localPlayerNum == _buzzedPlayer;
                _answerButtons[i].interactable = isMyTurn && _gameState == 3;
                if (_answerImages[i] != null)
                {
                    _answerImages[i].color = isMyTurn ? _cNormal : _cDim;
                }
            }
            else if (_gameState == 4)
            {
                // Reveal
                _answerButtons[i].interactable = false;
                if (_answerImages[i] != null)
                {
                    if (i == _lastCorrect) _answerImages[i].color = _cCorrect;
                    else if (i == _lastAnswerIdx && _lastAnswerIdx != _lastCorrect) _answerImages[i].color = _cWrong;
                    else _answerImages[i].color = _cDim;
                }
            }
        }

        // Trigger entrance animation
        if (_gameState == 1 && _animPhase == 0)
        {
            _animPhase = 1;
            _animTimer = 0f;
        }
    }

    private void HideQuestion()
    {
        if (_questionText != null) _questionText.text = "";
        if (_categoryText != null) _categoryText.text = "";
        for (int i = 0; i < AnswerCount; i++)
        {
            if (_answerButtons[i] != null) _answerButtons[i].gameObject.SetActive(false);
        }
    }

    // --- Helper methods ---
    private bool IsPlayerJoined(int p)
    {
        switch (p)
        {
            case 1: return _p1Id >= 0;
            case 2: return _p2Id >= 0;
            case 3: return _p3Id >= 0;
            case 4: return _p4Id >= 0;
            default: return false;
        }
    }

    private int GetScore(int p)
    {
        switch (p)
        {
            case 1: return _p1Score;
            case 2: return _p2Score;
            case 3: return _p3Score;
            case 4: return _p4Score;
            default: return 0;
        }
    }

    private void SetScore(int p, int val)
    {
        switch (p)
        {
            case 1: _p1Score = val; break;
            case 2: _p2Score = val; break;
            case 3: _p3Score = val; break;
            case 4: _p4Score = val; break;
        }
    }

    private int GetPlayerId(int p)
    {
        switch (p)
        {
            case 1: return _p1Id;
            case 2: return _p2Id;
            case 3: return _p3Id;
            case 4: return _p4Id;
            default: return -1;
        }
    }

    private void SetPlayerId(int p, int id)
    {
        switch (p)
        {
            case 1: _p1Id = id; break;
            case 2: _p2Id = id; break;
            case 3: _p3Id = id; break;
            case 4: _p4Id = id; break;
        }
    }

    private string GetPlayerName(int p)
    {
        switch (p)
        {
            case 1: return _p1Name;
            case 2: return _p2Name;
            case 3: return _p3Name;
            case 4: return _p4Name;
            default: return "";
        }
    }

    private void SetPlayerName(int p, string name)
    {
        switch (p)
        {
            case 1: _p1Name = name; break;
            case 2: _p2Name = name; break;
            case 3: _p3Name = name; break;
            case 4: _p4Name = name; break;
        }
    }

    private string GetWinnerName()
    {
        int maxScore = Mathf.Max(_p1Score, _p2Score, _p3Score, _p4Score);
        if (maxScore == _p1Score && _p1Id >= 0) return _p1Name;
        if (maxScore == _p2Score && _p2Id >= 0) return _p2Name;
        if (maxScore == _p3Score && _p3Id >= 0) return _p3Name;
        if (maxScore == _p4Score && _p4Id >= 0) return _p4Name;
        return "NO ONE";
    }

    private int FindPlayerSlot(int playerId)
    {
        if (_p1Id == playerId) return 1;
        if (_p2Id == playerId) return 2;
        if (_p3Id == playerId) return 3;
        if (_p4Id == playerId) return 4;
        return 0;
    }

    private int FindOpenSlot()
    {
        if (_p1Id < 0) return 1;
        if (_p2Id < 0) return 2;
        if (_p3Id < 0) return 3;
        if (_p4Id < 0) return 4;
        return 0;
    }

    // --- Network Events ---
    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (Networking.IsOwner(gameObject))
        {
            // Check if they were already in a slot (rejoin)
            int existing = FindPlayerSlot(player.playerId);
            if (existing > 0)
            {
                SetPlayerName(existing, player.displayName);
                RequestSerialization();
            }
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (Networking.IsOwner(gameObject))
        {
            int slot = FindPlayerSlot(player.playerId);
            if (slot > 0)
            {
                SetPlayerId(slot, -1);
                SetPlayerName(slot, "");
                _activePlayers--;
                if (_activePlayers < 0) _activePlayers = 0;
                RequestSerialization();
                ApplyState(false);
            }
        }
    }

    // --- Public events (called by buttons) ---
    public void JoinP1() { RequestJoin(1); }
    public void JoinP2() { RequestJoin(2); }
    public void JoinP3() { RequestJoin(3); }
    public void JoinP4() { RequestJoin(4); }

    private void RequestJoin(int slot)
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        // Check if already in a slot
        if (FindPlayerSlot(local.playerId) > 0) return;
        // Check if slot is open
        if (GetPlayerId(slot) >= 0) return;
        // Set the join request synced fields and take ownership
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(local, gameObject);
        }
        _joinRequestPlayerId = local.playerId;
        _joinRequestSlot = slot;
        _pendingJoinSlot = slot;
        _pendingJoinTimer = 2f;
        _localPlayerNum = slot; // Optimistic — confirmed when OnDeserialization runs
        RequestSerialization();
        ApplyState(false);
    }

    public void BuzzP1() { SendCustomNetworkEvent(NetworkEventTarget.Owner, "NetBuzzP1"); }
    public void BuzzP2() { SendCustomNetworkEvent(NetworkEventTarget.Owner, "NetBuzzP2"); }
    public void BuzzP3() { SendCustomNetworkEvent(NetworkEventTarget.Owner, "NetBuzzP3"); }
    public void BuzzP4() { SendCustomNetworkEvent(NetworkEventTarget.Owner, "NetBuzzP4"); }

    public void AnswerA() { SendCustomNetworkEvent(NetworkEventTarget.Owner, "NetAnswerA"); }
    public void AnswerB() { SendCustomNetworkEvent(NetworkEventTarget.Owner, "NetAnswerB"); }
    public void AnswerC() { SendCustomNetworkEvent(NetworkEventTarget.Owner, "NetAnswerC"); }
    public void AnswerD() { SendCustomNetworkEvent(NetworkEventTarget.Owner, "NetAnswerD"); }

    public void StartGame() { SendCustomNetworkEvent(NetworkEventTarget.All, "NetStartGame"); }
    public void ResetGame() { SendCustomNetworkEvent(NetworkEventTarget.All, "NetResetGame"); }

    // --- Networked game logic ---
    public void NetStartGame()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (_activePlayers == 0) return;

        _gameState = 1;
        _round = 1;
        _p1Score = 0;
        _p2Score = 0;
        _p3Score = 0;
        _p4Score = 0;
        _buzzedPlayer = 0;
        _secondChance = 0;
        _currentQuestionIdx = 0;

        // Shuffle question order
        int totalQ = _questions_text.Length;
        _questionOrder = new int[totalQ];
        for (int i = 0; i < totalQ; i++) _questionOrder[i] = i;
        for (int i = totalQ - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = _questionOrder[i];
            _questionOrder[i] = _questionOrder[j];
            _questionOrder[j] = temp;
        }

        RequestSerialization();
        ApplyState(true);
        Debug.Log("[ChromixTrivia] Game started. " + _activePlayers + " players.");
    }

    public void NetResetGame()
    {
        if (!Networking.IsOwner(gameObject)) return;
        _gameState = 0;
        _round = 0;
        _p1Score = 0;
        _p2Score = 0;
        _p3Score = 0;
        _p4Score = 0;
        _buzzedPlayer = 0;
        _secondChance = 0;
        _currentQuestionIdx = 0;
        _lastAnswerIdx = -1;
        _lastCorrect = -1;
        _revealTimer = 0;
        _animPhase = 0;
        RequestSerialization();
        ApplyState(true);
        Debug.Log("[ChromixTrivia] Game reset.");
    }

    // Owner processes join requests when it receives the synced data
    private void ProcessJoinRequest()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (_joinRequestPlayerId < 0 || _joinRequestSlot <= 0) return;
        int slot = _joinRequestSlot;
        int playerId = _joinRequestPlayerId;
        // Validate: slot must be open, player must not already be in a slot
        if (GetPlayerId(slot) >= 0) { _joinRequestPlayerId = -1; return; }
        if (FindPlayerSlot(playerId) > 0) { _joinRequestPlayerId = -1; return; }
        // Find the player's name
        VRCPlayerApi p = VRCPlayerApi.GetPlayerById(playerId);
        if (p == null || !p.IsValid()) { _joinRequestPlayerId = -1; return; }
        SetPlayerId(slot, playerId);
        SetPlayerName(slot, p.displayName);
        _activePlayers++;
        _joinRequestPlayerId = -1;
        RequestSerialization();
        ApplyState(false);
        Debug.Log("[ChromixTrivia] Owner assigned " + p.displayName + " to P" + slot);
        // Transfer ownership back to the master so the game owner stays consistent
        VRCPlayerApi master = VRCPlayerApi.GetPlayerById(1);
        if (master != null && master.IsValid() && master.playerId != playerId)
        {
            Networking.SetOwner(master, gameObject);
        }
    }

    // Owner-side buzz handlers
    public void NetBuzzP1() { OwnerBuzz(1); }
    public void NetBuzzP2() { OwnerBuzz(2); }
    public void NetBuzzP3() { OwnerBuzz(3); }
    public void NetBuzzP4() { OwnerBuzz(4); }

    private void OwnerBuzz(int playerNum)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (_gameState != 1) return; // Only buzz during question phase
        if (GetPlayerId(playerNum) < 0) return; // Slot must be occupied
        _buzzedPlayer = playerNum;
        _gameState = 3;
        _buzzCooldown = 0.5f;
        RequestSerialization();
        ApplyState(false);
        Debug.Log("[ChromixTrivia] P" + playerNum + " buzzed in!");
    }

    // Owner-side answer handlers
    public void NetAnswerA() { OwnerAnswer(0); }
    public void NetAnswerB() { OwnerAnswer(1); }
    public void NetAnswerC() { OwnerAnswer(2); }
    public void NetAnswerD() { OwnerAnswer(3); }

    private void OwnerAnswer(int answerIdx)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (_gameState != 3) return;

        int qIdx = _questionOrder[_currentQuestionIdx];
        int correct = _questions_correct[qIdx];
        _lastAnswerIdx = answerIdx;
        _lastCorrect = correct;
        _gameState = 4;
        _revealTimer = 180; // ~3 seconds at 60fps

        if (answerIdx == correct)
        {
            // Correct! +10 points
            int score = GetScore(_buzzedPlayer);
            SetScore(_buzzedPlayer, score + 10);
            _animPhase = 3;
            _animTimer = 0f;
        }
        else
        {
            // Wrong - give second chance to others
            int mask = 0;
            for (int i = 1; i <= MaxPlayers; i++)
            {
                if (i == _buzzedPlayer) continue;
                if (IsPlayerJoined(i)) mask |= (1 << (i - 1));
            }
            _secondChance = mask;
        }

        _animPhase = 2;
        _animTimer = 0f;
        RequestSerialization();
        ApplyState(false);
        Debug.Log("[ChromixTrivia] P" + _buzzedPlayer + " answered " + answerIdx + ". Correct=" + correct);
    }

    private void NextRound()
    {
        if (!Networking.IsOwner(gameObject)) return;

        _buzzedPlayer = 0;
        _secondChance = 0;
        _lastAnswerIdx = -1;
        _lastCorrect = -1;
        _revealTimer = 0;

        // Check if anyone can still answer (second chance)
        if (_secondChance != 0)
        {
            _gameState = 1;
            RequestSerialization();
            ApplyState(false);
            return;
        }

        _round++;
        if (_round > _maxRounds)
        {
            _gameState = 5;
            RequestSerialization();
            ApplyState(true);
            Debug.Log("[ChromixTrivia] Game over! Winner: " + GetWinnerName());
            return;
        }

        _currentQuestionIdx++;
        if (_currentQuestionIdx >= _questions_text.Length) _currentQuestionIdx = 0;
        _gameState = 1;
        RequestSerialization();
        ApplyState(true);
        Debug.Log("[ChromixTrivia] Next round: " + _round);
    }

    public override void OnDeserialization()
    {
        // Owner processes any pending join request
        ProcessJoinRequest();
        // Update local player number based on synced IDs
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local != null)
        {
            _localPlayerNum = FindPlayerSlot(local.playerId);
        }
        ApplyState(false);
    }
}
