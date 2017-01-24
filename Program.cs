using System;
using System.Collections.Generic;
using System.Linq;

// Forked off from https://dotnetfiddle.net/Wt0Up8 at its inception (1.0)

namespace ai {
    /* Phase 3 : AI
	* FRAMEWORK:
	* X) Player class needs a bool cpu = false
	* X) "Enter name" in Player constructor needs to parse for "(CPU)" as the first 5 chars and if true, mark cpu = true
	* 3) Create new AI class, these objects will belong to the players
	*  b) Needs "consultAI" method to make general decisions, should be passed self & opponent
	*  c) Needs "consultCharAI" method to check player's character and probe its specific AI preferences (fwds to charAI below)
	*    i) EACH char ability needs an AI version
	*  d) Base AI ALSO needs an "AIresponse" method that can answer specific requests from human opponent (Spray Fire & Seizmo)
	*  e) Needs basicFireAI that will never recur because it doesn't fire on already fired nor illegal targets,
	*     and resolves and reports to itself its own results
	* 4) Each character needs a "charAI" method that will make character-based decisions when called from player's AI
	 * No char?
	 * Val?
	 * Earl?
	 * Burt?
	 * Grady?
	* 5) If cpu == true at character setup, instantiate AI for the player and skip adding menuOptions
	* 6) EVERY prompt for input needs to check cpu == true and if so consultAI for the decision IN LIEU of prompting
	* 7) A standalone function to place graboids randomly for setup
	* 
	* BASE LOGIC FOR AI CLASS:
	* 1) INITIALIZE
	*  a) blanket(width) will create a list of coords that checkerboards a board, and do nothing else, just stash the list
	*	 i)   Checkerboard is randomly determined to be starting with 0,0 or 0,1 and checker from there
	*	 ii)  Whenever you're seeking, only shoot into the blanket, remove blanket items as they are fired on
	*	 iii) Use "width" to determine how far apart diagonally the blanket should be drawn... After DirtD killed, the
	*	      blanket needs to be widened and redrawn because the next smallest graboid is 3 spaces, etc.
	*  b) int currentHits = 0, every time a hit is returned, currentHits++ (EVENT)
	*  c) bool killed = false, true when killed (EVENT)
	*  d) string mode = seek
	*  e) int strategy = *random
	*  f) int[] seekDirection, can be 1, 0, or -1, determining whether that axis is unchanging, or which dir advancing
	*  g) int[] killDirection, can be 1, 0, or -1, determining whether that axis is unchanging, or which dir advancing
	*  h) list<int[]> seekSet
	*  i) list<int[]> fireQueue
	*  j) list<int[]> hitSet
	*  k) pop(int[], list) will delete a matching coord in given list (always pop blanket and seekSet/hitSet/fireQueue)
	* 2) bool consultAI(Player self, Player opponent) method - returns "done" to turn loop
	*  a) if mode == seek, seek(), else if mode == destroy, destroy();
	* 3) SEEK mode
	*  a) consultCharAI for preferences first
	*  b) Execute STRATEGY sub mode (asterisks are randomized elem)
	*  c) int[] newSeek() will randomly choose starting pt from the blanket to start with (given no charAI overrides)
	*	  then scan blanket's values for coords that fit a strategy below & populates seekSet w/ dupes of qualified items
	*	 i)   Single *row/column, blanket across *n/e/s/w as seekDirection, *newSeek when seekSet is depleted
	*	 ii)  Double *row/column, blanket across *n/e/s/w as seekDirection, *newSeek when seekSet is depleted
	*	 iii) Diagonal cuts, blanket *ne/se/sw/nw, *newSeek when seekSet is depleted
	*	 iv)  Prioritize corners *ne/se/sw/nw (ranges 7-9, 0-2), then switch strats to i-iii when range exhausted
	*	 v)   Prioritize edges *n/e/s/w (ranges ((0-2 or 7-9)x 3-6)), then switch strats to i-iii when range exhausted
	*	 vi)  Prioritize center (ranges 3-6), then switch strats to i-iii when range exhausted
	*  d) Randomly choose from seekSet and Fire
	*	 i)  If missed, iterate the seekSet next turn
	*    ii) If hit, pass XY into hitSet, currentHits++, switch to destroy mode
	* 4) DESTROY mode
	*  ** COPIOUSLY check for !gameOver after hits
	*  a) consultCharAI for preferences first
	*  b) If hitSet.count == 1 && fireQueue.count == 0
	*    i)  Add all spaces adjacent to hitSet[0] *n/e/s/w to fireQueue, Fire randomly *n/e/s/w
	*    ii) If hitSet.count == 1 > iterate fireQueue until another is a hit push that to hitSet
	*  c) If hitSet.count == 2 && killDirection == null
	*    i)   Compare hitSet[0] and hitSet[1], series of Ifs to determine horiz or vert (see setCoords validation)
	*    ii)  Clear fireQueue to flush adjacents from first shot
	*    iii) Set killDirection if not null, increment one more coord in that direction from *hitSet & add to fireQueue
	*  d) Fire on next in fireQueue
	*     - hit
	*       -> kill false? continue (increment one more coord in killDirection, fire)
	*       -> kill true? (proceed to step "e" below)
	*     - miss -> flip +1/-1 killDirection for next turn (multiply both coords by -1, cuz it'll be like [0,1])
	*  e) If killed = Y: currentHits - maxHp graboid killed; if currentHits == 0 (Confirming you didn't hit 2 diff graboids)
	*    i)  true -> RESET: killed = false; clear fireQueue; clear hitSet; mode = seek
	*    ii) false -> (some algorithm to search the full perimeter of your hitSet)
	* 
	*  PLACING GRABOIDS:
	*  
	*/
}

namespace Seizmojigger {
//    using ai;

    class Program {
        // Global functions

        static bool debug;
        static char gameModeShowBoard;
        static int gameModeNoPlayers;

        // Converts easy coordinates X# to array coordinates #,#; e.g. B1 to 1,0; Takes X#, returns ints [x,y]
        public static int[] ANtoXY(string an) {
            // BEAR IN MIND: In real Battleship, coordinates X# are *actually* Y,X coordinates, going to a row then column!!
            int[] xy = new int[2];

            // -- Bad X or Y coordinates in this function should always return [-1,-1]
            // Sanity check - string length
            if(an.Length != 2) {
                xy[0] = -1;
                xy[1] = -1;
            }

            // Don't bother converting if it already failed the first length check
            if(!xy.SequenceEqual(new int[] { -1, -1 })) {
                // Set X - convert character to uppercase, then to digit
                an = an.ToUpper();
                xy[0] = (int) (an[0] - 65);

                // Set Y - slightly more complex, expect a number and if you don't get one, set it to -1 to fail later
                // Bear in mind that Ys go 0-9, not 1-10, because it's easier to display in ascii
                int y;
                int.TryParse(an.Substring(1, 1), out y);
                if(int.TryParse(an.Substring(1, 1), out y)) {
                    xy[1] = y;
                } else {
                    xy[1] = -1;
                }

                // Be sure the final converted coordinates are still in range
                if(xy[0] < 0 || xy[0] > 9 || xy[1] < 0 || xy[1] > 9) {
                    xy[0] = -1;
                    xy[1] = -1;
                }
            }

            // If there were any errors, report it
            if(xy.SequenceEqual(new int[] { -1, -1 })) {
                Console.Write("Illegal grid coordinates! Must be in X# format, where X is [A-J], and # is [0-9].");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(" Retry:\n");
                Console.ResetColor();
            }

            // Always return, because the calling function should loop when [-1,-1] is returned
            return xy;
        }

        // Fetches coordinates for any reason from user
        public static int[] getCoords() {
            // Initialize coordinates as a fail value
            int[] convertedCoords = new int[2] { -1, -1 };

            // Keep asking for new coordinates until a valid coordinate in the grid is passed
            do {
                string givenCoords = Console.ReadLine();
                convertedCoords = ANtoXY(givenCoords);
            } while(convertedCoords[0] == -1 && convertedCoords[1] == -1);

            return convertedCoords;
        }

        // Fetches the set of all adjacent coordinates; Takes [x,y], returns all eligible adjacent coords
        public static int[][] getAdjCoords(int[] c) {
            // If [-1,-1] was passed, quit now
            if(c.SequenceEqual(new int[] { -1, -1 })) {
                return null;
            }

            // Take given coords, and adjust 1 in each direction
            List<int[]> adjList = new List<int[]>();
            adjList.Add(new int[] { c[0] - 1, c[1] });    // North
            adjList.Add(new int[] { c[0], c[1] + 1 });    // East
            adjList.Add(new int[] { c[0] + 1, c[1] });    // South
            adjList.Add(new int[] { c[0], c[1] - 1 });    // West

            // Count the valid ones only
            int validC = 0;
            foreach(int[] cn in adjList) {
                if(cn[0] > -1 && cn[0] < 10 && cn[1] > -1 && cn[1] < 10) {
                    validC++;
                }
            }

            // Add the valid ones only to a fresh array of coords
            int[][] adj = new int[validC][];
            int i = 0;
            foreach(int[] cn in adjList) {
                if(cn[0] > -1 && cn[0] < 10 && cn[1] > -1 && cn[1] < 10) {
                    adj[i] = cn;
                    i++;
                }
            }

            return adj;
        }

        // Defines a menu item
        public struct menuItem {
            public string label;    // Label within an options menu
            public Action method;   // Method to run
            public bool endsTurn;   // Whether or not menu should reload turn after running method

            public menuItem(string l, Action m, bool e = true) {
                label = l;
                method = m;
                endsTurn = e;
            }
        }

        // Checks a space on the board; Takes [i,i] coordinates, returns [hit/miss,graboidType]
        public static string[] target(Board toBoard, int[] c) {
            string[] results = new string[2];
            results[0] = toBoard.grid[c[0], c[1]].state;
            results[1] = toBoard.grid[c[0], c[1]].graboidType;
            return results;
        }

        // Class definitions

        // -- Board classes

        public class Space {
            public string graboidType { get; set; } // 5-char code for what graboid is here, if any, default "null"
            public string state { get; set; } // Use "null","miss","hit" / possibly switch this to enums?

            // Constructor
            public Space() {
                this.graboidType = null;
                this.state = null;
            }
        }

        public class Board {
            public Space[,] grid = new Space[10, 10]; // A 10 x 10 array of spaces

            // Constructor
            public Board() {
                // Initialize array with spaces
                for(int x = 0; x < 10; x++) {
                    for(int y = 0; y < 10; y++) {
                        grid[x, y] = new Space();
                    }
                }
            }

            // Displays the board in ascii
            public void showAll(char with = 'n') {
                for(int lat = 0; lat < 10; lat++) {
                    Console.Write("| ");
                    for(int lon = 0; lon < 10; lon++) {
                        // Label spaces X#
                        char L = (char) (lat + 65);
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write("{0}{1}", L, lon);
                        Console.ResetColor();

                        // Display states
                        if(this.grid[lat, lon].state == null) {
                            Console.Write("  ");
                        } else if(this.grid[lat, lon].state == "hit") {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write(" X");
                            Console.ResetColor();
                        } else if(this.grid[lat, lon].state == "miss") {
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.Write(" o");
                            Console.ResetColor();
                        }

                        // If "with" is 'g' for 'graboids', show graboids and allow space for graboid names
                        if(with == 'g' && this.grid[lat, lon].graboidType != null) {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write(" " + this.grid[lat, lon].graboidType); // write out the 5 char code
                            Console.ResetColor();
                        } else if(with == 'g' && this.grid[lat, lon].graboidType == null) {
                            Console.Write("      "); // space + 5 spaces for 5 char code blank
                        }

                        // Separate columns
                        Console.Write("| ");
                    }

                    Console.WriteLine();
                }
            }
        }

        // -- Graboid classes

        public class Graboid {
            private string _type;
            private int _hp;
            public readonly int maxHp;
            private string _displayName;

            public string type { get { return _type; } }
            public int hp { get { return _hp; } }
            public string displayName { get { return _displayName; } }

            public int[][] coordinates { get; set; } // Read as [hp# instances of, [x,y] grid coordinate pair arrays]

            // Constructor
            public Graboid(string code) {
                _type = code;
                switch(type) {
                    case "dirtd":
                        this._displayName = "Dirt Dragon";
                        this._hp = 2;
                        this.maxHp = 2;
                        break;
                    case "shrkr":
                        this._displayName = "Shrieker";
                        this._hp = 3;
                        this.maxHp = 3;
                        break;
                    case "assbl":
                        this._displayName = "Ass Blaster";
                        this._hp = 3;
                        this.maxHp = 3;
                        break;
                    case "grabd":
                        this._displayName = "Graboid";
                        this._hp = 4;
                        this.maxHp = 4;
                        break;
                    case "blanc":
                        this._displayName = "El Blanco";
                        this._hp = 5;
                        this.maxHp = 5;
                        break;
                    default:
                        Console.WriteLine("ERROR: Invalid graboid type");
                        break;
                }
                coordinates = new int[hp][];
            }

            // Takes a hit, then returns "true" if it died
            public bool takeHit() {
                _hp--;

                // Show HP notice if debug is on
                if(debug) {
                    Console.WriteLine("{0} took a hit, {1} hp left.", this._displayName, this._hp);
                }

                if(_hp <= 0) {
                    return true;
                }
                return false;
            }
        }

        // -- Ability classes -- UNUSED, but leaving in case I come back to the idea

        // A base class/delegate for a passive ability
        // A base class/delegate for an activated ability
        // character (which holds menuOptions) needs property: #uses, if uses == 0 ? remove option

        // -- Generic Character classes

        // Basic firing routine; Takes a Player, asks for coords, resolves the whole 9 yards
        public static void basicFire(Player p, int[] givenC = null) {
            int[] c;
            char a;
            // If coordinates are given, use those, otherwise prompt for some
            if(givenC != null) {
                c = givenC;
                a = (char) (c[0] + 65);
                Console.Write("Firing on {0}{1}: ", a, c[1]);
            } else {
                Console.Write("Fire: ");
                c = getCoords();
                a = (char) (c[0] + 65);
            }

            // On state:null, check for graboid and hit or miss it; On state:hit/miss, cancel
            string[] targetStatus = target(p.board, c);
            switch(targetStatus[0]) {
                case null:
                    // On graboid present, hit; else, miss
                    if(targetStatus[1] != null) {
                        p.board.grid[c[0], c[1]].state = "hit";
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\nHit!!");
                        Console.ResetColor();
                        // Prod player to broadcast a hit event
                        p.character.getHit();
                        p.dmgGraboid(targetStatus[1]);
                    } else {
                        p.board.grid[c[0], c[1]].state = "miss";
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine("\nMiss...");
                        Console.ResetColor();
                    }
                    break;
                default:
                    // Convert XY to AN for screen printing purposes				
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("You've already fired on {0}{1}, it was a {2}. Select new coordinates:",
                                        a, c[1], targetStatus[0]);
                    Console.ResetColor();
                    basicFire(p);
                    break;
            }
        }

        // Delegates for alerts
        // DELEGATES ARE PROXIES OF OTHER FUNCTIONS
        // http://stackoverflow.com/questions/2814065/help-understanding-net-delegates-events-and-eventhandlers
        public delegate void aGraboidHit();
        public delegate void aGraboidDied();
        public delegate void ability();                 // An ability that is self-contained/affects the self
        public delegate void abilityO(Player opponent); // An ability that needs a target opponent designated

        // Generic Character template; allows pointing to fire function, and menu options generation
        public abstract class Character {
            public abstract void fire(Player p, int[] givenC);
            public abstract void profile(Player p);
            // Specific characters' profiles should start with their name, then list abilities
            // and label whether each is a passive ability, or an active with X uses left
            public ability ability1;    // The numbers are constant, maintain #s per slot: 1st abil, 2nd abil
            public ability ability2;    // The "ability" delegate is for no-parameter abilities
            public abilityO abilityO1;  // The "abilityO"'s are optional, in case the ability (1st
            public abilityO abilityO2;  // or 2nd slot) targets an Opponent as a parameter
            public List<menuItem> menuOptions = new List<menuItem>();
            public event ability graboidHit;
            public event ability graboidDied;

            public void getHit() {
                // If there's no subscribers to graboidHit, then there's no reason to fire it (causes errors)
                if(graboidHit != null) {
                    graboidHit();
                }
            }
            public void getKilled() {
                // If there's no subscribers to graboidDied, then there's no reason to fire it (causes errors)
                if(graboidDied != null) {
                    graboidDied();
                }
            }

            // Show options
            /* Displays available options for the character, and prompts for input. Accepts a default input of X#
                * format and directly passes to fire, else executes the chosen method, returns whether or not the turn
                * should continue based on the action taken, and reloads the menu options if the turn is not over. */
            public virtual bool options(Player opponent) {
                Console.WriteLine("Enter coordinates to fire on, or choose an option below:");
                for(int i = 0; i < menuOptions.Count; i++) {
                    Console.WriteLine("{0}) {1}", i + 1, menuOptions[i].label);
                }
                Console.Write("Command >> ");
                bool success = false;
                int input = 0;
                string rawInput = Console.ReadLine();

                if(rawInput.Length == 2) {
                    // Got 2 chars? Fire
                    int[] xy = ANtoXY(rawInput);
                    while(xy[0] == -1) {
                        xy = getCoords();
                    }
                    fire(opponent, xy);
                    return true;
                } else if(rawInput.Length == 1) {
                    // Got 1 char? Do that menu option
                    success = int.TryParse(rawInput, out input);

                    // If they enter something outside of the # of options, repeat turn
                    if(input < 1 || input > menuOptions.Count) {
                        success = false;
                    }
                }

                if(!success) {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Unrecognized input.");
                    Console.ResetColor();
                    return false;
                }

                // If they didn't fire, adjust the input and execute the chosen menuOption
                input--;
                // Need to record whether or not this ability will end the turn, BEFORE executing the ability
                bool done = menuOptions[input].endsTurn;
                /* WHY: If an ability runs out of uses, it removes itself from the menu as an option for
                    *		the future. This makes menuOptions[input] point to the NEXT menu option down, and
                    *		then read "did the method end the turn?" from THAT option, which can allow a
                    *		character to get an extra turn upon depleting an active ability. */
                menuOptions[input].method();
                return done;
            }

            public void disableOption(string label, string msg) {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(msg);
                Console.ResetColor();
                menuOptions.Remove(menuOptions.Single(o => o.label == label));
            }
        }

        // -- Specific Character classes

        // No Character
        public class noCharacter : Character {
            public override void fire(Player p, int[] givenC) {
                basicFire(p, givenC);
            }

            public override void profile(Player p) {
                Console.Write("Critical, NEED TO KNOW information: ");
                p.writeName();
                Console.Write(" isn't using a character, smart guy.\n"
                    + "a.k.a. Think of me as regular Battleship by Hasbro (née Milton Bradley)");
            }
        }

        // Valentine McKee
        public class valentineMcKee : Character {
            private int rage = 0;
            private int deadGraboids = 0;
            private int shots = 0;

            public override void fire(Player p, int[] givenC) {
                // Fire until spent
                while(shots > 0 && !gameOver) {
                    basicFire(p, givenC);
                    if(givenC != null) { givenC = null; }
                    shots--;
                    if(shots > 0 && !gameOver) {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write("\nRAGE! ");
                        Console.ResetColor();
                        p.showBoard();
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write("{0} shot(s) remaining. ", shots);
                        Console.ResetColor();
                    }
                }

                // Reset rage counter
                rage = 0;
            }

            public override bool options(Player opponent) {
                // Calculate shots
                shots = deadGraboids;
                if(shots < 1) {
                    shots = 1;
                }
                shots += rage;
                if(shots > 1) {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("There's enough rage in you for {0} shots this round.", shots);
                    Console.ResetColor();
                }
                bool done = base.options(opponent);
                return done;
            }

            // Constructor
            public valentineMcKee() {
                ability1 = new ability(dontGDpushMe);
                ability2 = new ability(fYou);
            }

            // Ability 1 - Passive - Don't You GD Push Me
            public void dontGDpushMe() {
                deadGraboids++;
                //			Console.WriteLine("Dead graboids {0}", deadGraboids);
            }

            // Ability 2 - Passive - Fuuuuu YOU
            public void fYou() {
                this.rage++;
                //			Console.WriteLine("Rage {0}", rage);
            }

            public override void profile(Player p) {
                Console.Write("Critical, NEED TO KNOW information about ");
                Console.BackgroundColor = p.myColor;
                Console.Write("Valentine McKee");
                Console.ResetColor();
                Console.WriteLine(":\n\n"
                + "Valentine McKee is a drifter with a foul mouth, just trying to scrape by taking any work he can\n"
                + "get his hands on. Known for his short temper and reactionary demeanor, few people cross Val if\n"
                + "they don't have to.");

                Console.WriteLine("\nAbilities:\n");

                int estShots = deadGraboids;
                if(estShots < 1) { estShots = 1; }
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Don't You GD Push Me (Passive)");
                Console.ResetColor();
                Console.WriteLine("Each turn, Val fires as many times as he has dead graboids, with a minimum of 1.\n" +
                                "[Current base fire: {0} shot(s)]", estShots);
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Fuuuuu-- YOU! (Passive)");
                Console.ResetColor();
                Console.WriteLine("Val fires an additional shot for every hit his graboids suffered in the previous turn."
                                + "\n[Current rage bonus: {0} additional shot(s)]", rage);
                Console.WriteLine();
            }
        }

        // Earl Bassett
        public class earlBassett : Character {
            public override void fire(Player p, int[] givenC) {
                basicFire(p, givenC);
            }

            public override bool options(Player opponent) {
                // Check for seizmojigger located guaranteed hit
                if(seizmoLocated != null) {
                    string[] targetStatus = target(opponent.board, seizmoLocated);
                    // If the location has already been hit, nevermind and clear located
                    if(targetStatus[0] != null) {
                        seizmoLocated = null;
                    } else {
                        // Otherwise, display the location
                        char a = (char) (seizmoLocated[0] + 65);
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("The seizmojigger has located a graboid at {0}{1}! ", a, seizmoLocated[1]);
                        Console.ResetColor();
                    }
                }
                bool done = base.options(opponent);
                return done;
            }

            // Constructor
            public earlBassett() {
                abilityO1 = new abilityO(rcBigfootBomb);
                abilityO2 = new abilityO(seizmojigger);
            }

            // Ability 1 - Active - R/C Bigfoot Bomb
            private int rcBombs = 3;
            private int rcSteps = 6;
            public void rcBigfootBomb(Player p) {
                // Initiate steps & recursive subroutine, preset to no previous coords (indicated by [-1,-1])
                rcShot(p, new int[] { -1, -1 }, rcSteps);

                // Decrement R/C Bombs, and disable ability if it's used up
                rcBombs--;
                if(rcBombs == 0) {
                    disableOption("R/C Bigfoot Bomb", "No more R/C bombs!");
                }
            }

            // R/C Bigfoot Bomb, part 2, recursive routine to choose adjacent coordinates
            public void rcShot(Player p, int[] prev, int steps) {
                int[][] adj = getAdjCoords(prev);   // Coordinates adjacent to previous shot (if any, else null)
                int[] c;                            // Current coordinates chosen

                // If this was passed prev coords
                if(adj != null) {
                    bool validTargetsInAdj = false;
                    foreach(int[] v in adj) {
                        // If just one coord in adj is in grid and not fired on, valid targets exist
                        string[] untried = target(p.board, v);
                        if(untried[0] == null) {
                            validTargetsInAdj = true;
                            break;
                        }
                    }

                    // If there are no valid targets, quit
                    if(!validTargetsInAdj) {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Nowhere for R/C to run to! Self-destructing!");
                        Console.ResetColor();
                        return;
                    }
                }

                // Ask for new coordinates
                bool OK = false;
                do {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("R/C Bigfoot Bomb");
                    Console.ResetColor();
                    Console.Write(" ({0} shots remaining): ", steps);
                    c = getCoords();

                    // If there was a prev shot, be sure new coords are in our valid target set around it
                    if(adj == null) {
                        OK = true;
                    } else {
                        foreach(int[] v in adj) {
                            if(c.SequenceEqual(v)) {
                                OK = true;
                                break;
                            }
                        }
                    }

                    // Alert reminder if you weren't adjacent
                    if(!OK) {
                        char a = (char) (prev[0] + 65);
                        Console.WriteLine("You must select coordinates adjacent to {0}{1}.", a, prev[1]);
                    }
                } while(!OK);

                // Fire on new coords
                string[] targetStatus = target(p.board, c);

                // This part is copied from basicFire, but recurs rcShot if the space was already fired on or misses
                // On state:null, check for graboid and hit or miss it; On state:hit/miss, cancel
                switch(targetStatus[0]) {
                    case null:
                        // On graboid present, hit; else, miss
                        if(targetStatus[1] != null) {
                            p.board.grid[c[0], c[1]].state = "hit";
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\nHit!!");
                            Console.ResetColor();
                            // Prod player to broadcast a hit event
                            p.character.getHit();
                            p.dmgGraboid(targetStatus[1]);
                            return;
                        } else {
                            p.board.grid[c[0], c[1]].state = "miss";
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine("\nMiss...");
                            Console.ResetColor();
                            // Steps down, and if it's out, end the recursion and R/C Bomb
                            steps--;
                            if(steps == 0) {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("R/C ran out of juice...");
                                Console.ResetColor();
                                return;
                            }
                            // Otherwise, take the next step, using the current coords as prev
                            rcShot(p, c, steps);
                            return;
                        }
                    default:
                        // Convert XY to AN for screen printing purposes				
                        char a = (char) (c[0] + 65);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("You've already fired on {0}{1}, it was a {2}.",
                                            a, c[1], targetStatus[0]);
                        Console.ResetColor();
                        rcShot(p, prev, steps);
                        return;
                }
            }

            // Ability 2 - Active - Seizmojigger
            private int seizmo = 2;
            private int[] seizmoLocated = null;
            public void seizmojigger(Player p) {
                // Don't let the player use Seizmojigger twice in a row, they'll lose the previous located coords
                if(seizmoLocated != null) {
                    char a = (char) (seizmoLocated[0] + 65);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("The seizmojigger cannot be used again until you fire on {0}{1}.", a, seizmoLocated[1]);
                    Console.ResetColor();
                    fire(p, null);
                    return;
                }

                // Alert that the opponent handles this, then "end turn" (visually, in code the turn is still on)
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("Seizmojigger! ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Your turn is over, ");
                p.writeName();
                Console.WriteLine(" will reveal a graboid's coordinates on their turn.");
                Console.ResetColor();

                // Decrement sprays while player is still viewing screen, and disable ability if it's used up
                seizmo--;
                if(seizmo == 0) {
                    disableOption("Seizmojigger", "The seizmojigger's busted!");
                }
                Console.Write("\nTurn Over. ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Please LOOK AWAY from the screen and give control to ");
                p.writeName();
                Console.Write("! ");
                Console.ResetColor();
                Console.Write("Press any key to continue...");
                Console.ReadKey();
                Console.Clear();

                // Opponent gains control
                p.writeName();
                Console.Write(", your opponent used ");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Seizmojigger. ");
                Console.ResetColor();
                Console.WriteLine("You must reveal a coordinate that contains a graboid which has not yet been hit " +
                            "to your opponent.\n");
                Console.WriteLine("Select coordinates which will hit, in the format X#.");

                // Extracting to separate process to allow retries in case of an error
                opponentDisclosesSeizmojigger(p);
            }

            // Seizmojigger, part 2, opponent's responsibilities
            public void opponentDisclosesSeizmojigger(Player p) {
                // -- Collect input from user
                int[] disclosed = new int[2];
                p.showBoard('g');
                Console.Write("Disclose: ");
                disclosed = getCoords();

                // -- Validate
                int errors = 0;

                // Confirm that coords are an untried hit
                string[] untriedHit = target(p.board, disclosed);
                if(untriedHit[1] == null) {
                    Console.WriteLine("Coordinates are not occupied by a graboid and would be a miss.");
                    errors++;
                } else if(untriedHit[0] != null) {
                    Console.WriteLine("Coordinates already fired on!");
                    errors++;
                }

                // Prompt to try again if there were errors
                if(errors > 0) {
                    Console.WriteLine("Please re-select coordinates.");
                    opponentDisclosesSeizmojigger(p);
                    return;
                }

                // -- Finalize
                // Write validated, confirmed set of coordinates to seizmo's location
                seizmoLocated = disclosed;
                Console.WriteLine("Seizmojigger has located a graboid. Thank you!");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Standby, ");
                p.writeName();
                Console.Write("! ");
                Console.ResetColor();
                Console.WriteLine("You will be taking your turn next...");
                return;
            }

            public override void profile(Player p) {
                Console.Write("Critical, NEED TO KNOW information about ");
                Console.BackgroundColor = p.myColor;
                Console.Write("Earl Bassett");
                Console.ResetColor();
                Console.WriteLine(":\n\n"
                + "Earl Bassett is a hired hand, part time repairman, and seasonal ostrich ranch hand, turned reluctant\n"
                + "graboid exterminator. Cautious and calculating, Earl doesn't like to take chances - which has occasionally\n"
                + "lead to him missing some of his biggest opportunities.");

                Console.WriteLine("\nAbilities:\n");

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("R/C Bigfoot Bomb ({0} of 3 left)", rcBombs);
                Console.ResetColor();
                Console.WriteLine("Earl remote controls a toy 4x4 with dynamite strapped to it, which fires one normal\n" +
                    "shot, and if it misses, allows Earl to fire again in an adjacent coordinate. Earl continues to try\n" +
                    "again until a total of {0} shots have been attempted.", rcSteps);
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Seizmojigger ({0} of 2 left)", seizmo);
                Console.ResetColor();
                Console.WriteLine(
                    "Earl activates the seizmojigger sonar system, passing his turn and forcing the opponent to reveal\n" +
                    "a coordinate that has not yet been fired on, which will result in a hit for Earl. This space will be\n" +
                    "revealed to Earl the next time he chooses to Fire.");
                Console.WriteLine();
            }
        }

        // Burt Gummer
        public class burtGummer : Character {
            public override void fire(Player p, int[] givenC) {
                extraAmmo();
                do {
                    basicFire(p, givenC);
                    if(givenC != null) { givenC = null; }
                    ammo--;
                    if(ammo > 0 && !gameOver) {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write("\nExtra Ammo! ");
                        Console.ResetColor();
                        p.showBoard();
                    }
                } while(ammo > 0 && !gameOver);
            }

            // Constructor
            public burtGummer() {
                ability1 = new ability(extraAmmo);
                abilityO2 = new abilityO(clusterCharge);
            }

            // Ability 1 - Passive - Extra Ammo
            private int ammo = 0;
            public void extraAmmo() {
                this.ammo++;
            }

            // Ability 2 - Active - Cluster Charge
            private int charges = 2;
            public void clusterCharge(Player p) {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("Cluster Charge: ");
                Console.ResetColor();
                // Get center shot
                int[] c = getCoords();
                string[] targetStatus = target(p.board, c);

                // Copy pasted from basicFire, but recurs Cluster Charge if fired on an already fired space
                // On state:null, check for graboid and hit or miss it; On state:hit/miss, cancel
                switch(targetStatus[0]) {
                    case null:
                        // On graboid present, hit; else, miss
                        if(targetStatus[1] != null) {
                            p.board.grid[c[0], c[1]].state = "hit";
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\nHit!!");
                            Console.ResetColor();
                            p.character.graboidHit -= ability1; // Turn off Extra Ammo, Cluster Charge shouldn't trigger
                                                                // Prod player to broadcast a hit event (for anyone else listening for other reasons)
                            p.character.getHit();
                            p.dmgGraboid(targetStatus[1]);
                        } else {
                            p.board.grid[c[0], c[1]].state = "miss";
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine("\nMiss...");
                            Console.ResetColor();
                            p.character.graboidHit -= ability1; // Turn off Extra Ammo, Cluster Charge shouldn't trigger
                                                                // This handles accidental triggers in the surrounding coords
                        }
                        break;
                    default:
                        // Convert XY to AN for screen printing purposes				
                        char a = (char) (c[0] + 65);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("You've already fired on {0}{1}, it was a {2}.",
                                            a, c[1], targetStatus[0]);
                        Console.ResetColor();
                        clusterCharge(p);
                        return;
                }

                // Fire on all adjacent coordinates
                int[][] adj = getAdjCoords(c);
                foreach(int[] cn in adj) {
                    char a = (char) (cn[0] + 65);
                    Console.Write("Cluster Charge bursts damage to {0}{1}: ", a, cn[1]);
                    string[] cnStatus = target(p.board, cn);

                    // Copy pasted from basicFire, without recursion from firing on previous coordinates
                    // On state:null, check for graboid and hit or miss it; On state:hit/miss, cancel
                    switch(cnStatus[0]) {
                        case null:
                            // On graboid present, hit; else, miss
                            if(cnStatus[1] != null) {
                                p.board.grid[cn[0], cn[1]].state = "hit";
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("\nHit!!");
                                Console.ResetColor();
                                // Prod player to broadcast a hit event
                                p.character.getHit();
                                p.dmgGraboid(cnStatus[1]);
                            } else {
                                p.board.grid[cn[0], cn[1]].state = "miss";
                                Console.ForegroundColor = ConsoleColor.White;
                                Console.WriteLine("\nMiss...");
                                Console.ResetColor();
                            }
                            break;
                        default:
                            // Convert XY to AN for screen printing purposes
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("\nYou've already fired on {0}{1}, it was a {2}.", a, cn[1], cnStatus[0]);
                            Console.ResetColor();
                            break;
                    }
                }

                p.character.graboidHit += ability1; // Turn Extra Ammo listener back on

                // Decrement charges, and disable ability if it's used up
                charges--;
                if(charges == 0) {
                    disableOption("Cluster Charge", "Out of Cluster Charges!");
                }
            }

            public override void profile(Player p) {
                Console.Write("Critical, NEED TO KNOW information about ");
                Console.BackgroundColor = p.myColor;
                Console.Write("Burt Gummer");
                Console.ResetColor();
                Console.WriteLine(":\n\n"
                + "Burt Gummer is a firearms enthusiast, and a paranoid survivalist, who has only run out of ammo\n"
                + "*once*. He has an \"overkill\" approach to problem solving and takes himself deadly seriously,\n"
                + "much to the annoyance and/or amusement of others.");

                Console.WriteLine("\nAbilities:\n");

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Extra Ammo (Passive)");
                Console.ResetColor();
                Console.WriteLine("Burt fires normal shots until he misses.");
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Cluster Charge ({0} of 2 left)", charges);
                Console.ResetColor();
                Console.WriteLine(
                    "Fires a shot, which then additionally strikes above, below, and to the left and right of the chosen\n"
                    + "coordinates, in a + (plus) pattern.");
                Console.WriteLine();
            }
        }

        // Grady Hoover
        public class gradyHoover : Character {
            int shots = 1;
            public override void fire(Player p, int[] givenC) {
                do {
                    if(shots > 1) {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write("{0} shots remaining. ", shots);
                        Console.ResetColor();
                    }
                    basicFire(p, givenC);
                    if(givenC != null) { givenC = null; }
                    shots--;
                    if(shots > 0 && !gameOver) {
                        p.showBoard();
                    }
                } while(shots > 0 && !gameOver);

                // Reset shots
                shots = 1;
            }

            // Constructor
            public gradyHoover() {
                ability1 = new ability(waBam50gs);
                abilityO2 = new abilityO(sprayFire);
            }

            // Ability 1 - Passive - WA-BAM!! 50 G's
            public void waBam50gs() {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("WA-BAM!! 50 G's ");
                Console.ResetColor();
                shots += 5;
            }

            // Ability 2 - Active - Spray Fire
            private int sprays = 2;
            public void sprayFire(Player p) {
                // Alert that the opponent handles this, then "end turn" (visually, in code the turn is still on)
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("Spray Fire! ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Your turn is over, ");
                Console.ResetColor();
                p.writeName();
                Console.WriteLine(" will reveal 7 misses on their turn.");

                // Decrement sprays while player is still viewing screen, and disable ability if it's used up
                sprays--;
                if(sprays == 0) {
                    disableOption("Spray Fire", "Used up all the Spray Fire!");
                }
                Console.Write("\nTurn Over. ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Please LOOK AWAY from the screen and give control to ");
                p.writeName();
                Console.Write("! ");
                Console.ResetColor();
                Console.Write("Press any key to continue...");
                Console.ReadKey();
                Console.Clear();

                // Opponent gains control
                p.writeName();
                Console.Write(", your opponent used ");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Spray Fire. ");
                Console.ResetColor();
                Console.WriteLine("You must reveal 7 new misses to the opponent. No two of them may be adjacent to each\n" +
                            "other. You cannot select a coordinate fully surrounded by coordinates already fired on.");
                Console.WriteLine("Enter coordinates which will miss, one at a time, in the format X#.");

                // Extracting to separate process to allow retries in case of an error
                opponentDisclosesSprayFire(p);
            }

            // Spray Fire, part 2, opponent's responsibilities
            public void opponentDisclosesSprayFire(Player p) {
                // -- Collect input from user
                int[][] missArray = new int[7][];
                p.showBoard('g');
                for(int i = 0; i < 7; i++) {
                    Console.Write("Miss #{0}: ", i + 1);
                    missArray[i] = getCoords();
                }

                // -- Validate

                int errors = 0;

                // Check whether any coords are repeated
                for(int compare = 0; compare < 7 - 1; compare++) {
                    // Define how many coords left in the array need to be checked
                    int rest = 7 - compare - 1;
                    for(int i = 0; i < rest; i++) {
                        // Define the coords you're checking against
                        int against = 7 - rest + i;
                        if(missArray[compare].SequenceEqual(missArray[against])) {
                            char a = (char) (missArray[compare][0] + 65);
                            Console.WriteLine("{0}{1} is repeated!", a, missArray[compare][1]);
                            errors++;
                            break;
                        }
                    }
                    if(errors > 0) {
                        break;
                    }
                }

                // Confirm that all coords are untried misses
                foreach(int[] c in missArray) {
                    string[] untriedMisses = target(p.board, c);
                    if(untriedMisses[1] != null) {
                        char a = (char) (c[0] + 65);
                        Console.WriteLine("{0}{1} is occupied by a graboid and would be a hit! ({2})", a, c[1], untriedMisses[1]);
                        errors++;
                        break;
                    }
                    if(untriedMisses[0] != null) {
                        char a = (char) (c[0] + 65);
                        Console.WriteLine("{0}{1} has already been fired on!", a, c[1]);
                        errors++;
                        break;
                    }
                }

                // Check whether coords are adjacent to at least one other coord in the set
                for(int compare = 0; compare < 7; compare++) {
                    for(int against = compare + 1; against < 7; against++) {
                        // Generate array of coordinates adjacent to "against"
                        int[][] adjArray = getAdjCoords(missArray[against]);

                        // Check "compare" against each adjacent to "against"
                        foreach(int[] chk in adjArray) {
                            if(missArray[compare].SequenceEqual(chk)) {
                                char a = (char) (missArray[compare][0] + 65);
                                char b = (char) (missArray[against][0] + 65);
                                Console.WriteLine("{0}{1} and {2}{3} are adjacent!",
                                                  a, missArray[compare][1], b, missArray[against][1]);
                                errors++;
                                break;
                            }
                        }

                        if(errors > 0) { break; }
                    }

                    if(errors > 0) { break; }
                }

                // Check if all surrounding adjacent coords are already fired on
                for(int check = 0; check < 7; check++) {
                    // Generate array of coordinates adjacent to check
                    int[][] adjArray = getAdjCoords(missArray[check]);

                    // Check all for previously attempted shots
                    int nearbyFired = 0;
                    foreach(int[] chk in adjArray) {
                        string[] targetStatus = target(p.board, chk);
                        if(targetStatus[0] != null) {
                            nearbyFired++;
                        }
                    }

                    // Report errors
                    if(nearbyFired == adjArray.Count()) {
                        char a = (char) (missArray[check][0] + 65);
                        Console.WriteLine("All coordinates surrounding {0}{1} have already been fired on!", a, missArray[check][1]);
                        errors++;
                        break;
                    }
                }

                // Prompt to try again if there were errors
                if(errors > 0) {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Please re-select coordinates.");
                    Console.ResetColor();
                    opponentDisclosesSprayFire(p);
                    return;
                }

                // -- Finalize

                // Write validated, confirmed set of coordinates to given board's spaces
                foreach(int[] c in missArray) {
                    p.board.grid[c[0], c[1]].state = "miss";
                }

                // Confirm or start over; Display board first, which is why coords are written BEFORE confirmation
                ConsoleKeyInfo confirm;
                p.showBoard('g');
                do {
                    // Prompt for confirmation
                    Console.WriteLine("Is this selection of misses OK to disclose? Press Y to confirm, N to redo. ");
                    confirm = Console.ReadKey(true);
                } while(confirm.Key != ConsoleKey.Y && confirm.Key != ConsoleKey.N);

                if(confirm.Key == ConsoleKey.Y) {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("Standby, ");
                    p.writeName();
                    Console.Write("! ");
                    Console.ResetColor();
                    Console.WriteLine("You will be taking your turn next...");
                    return;
                } else if(confirm.Key == ConsoleKey.N) {
                    // Blank out previous choices from the board
                    foreach(int[] c in missArray) {
                        p.board.grid[c[0], c[1]].state = null;
                    }
                    // Start over
                    opponentDisclosesSprayFire(p);
                    return;
                }
            }

            public override void profile(Player p) {
                Console.Write("Critical, NEED TO KNOW information about ");
                Console.BackgroundColor = p.myColor;
                Console.Write("Grady Hoover");
                Console.ResetColor();
                Console.WriteLine(":\n\n"
                + "Grady, Grady, Grady Hoover is blessed with a sunny disposition, drives a Taxi, lives in a crappy apartment,\n"
                + "and watches too much T.V. A city slicker by nature, he has no combat skills to speak of, but has dreams to\n"
                + "one day become a famous graboid hunter and open a theme park.");

                Console.WriteLine("\nAbilities:\n");

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("WA-BAM!! 50 G's (Passive)");
                Console.ResetColor();
                Console.WriteLine("When Grady kills a graboid, he gets 5 extra shots immediately.");
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Spray Fire ({0} of 2 left)", sprays);
                Console.ResetColor();
                Console.WriteLine(
                    "Opponent must reveal 7 misses. No two coordinates revealed in Spray Fire can be adjacent to each\n" +
                    "other, and none can be in a coordinate that is surrounded by spaces already fired on.");
                Console.WriteLine();
            }
        }

        // -- Player classes

        public class Player {
            private readonly string name;
            public ConsoleColor myColor;
            private Board _board = new Board();
            private List<Graboid> _graboids = new List<Graboid>() {
            new Graboid("dirtd"),
            new Graboid("shrkr"),
            new Graboid("assbl"),
            new Graboid("grabd"),
            new Graboid("blanc")
        };

            public Board board { get { return _board; } }
            public List<Graboid> graboids { get { return _graboids; } }
            public Character character;
            private int characterSelected;
            private readonly bool cpu = false;

            // Constructor
            public Player(ConsoleColor playerColor) {
                // Set color
                myColor = playerColor;

                // Enter name
                Console.WriteLine("Enter your name: ");
                do {
                    name = Console.ReadLine();
                    if(name.Length < 1) {
                        Console.WriteLine("Retry: ");
                    }
                } while(name.Length < 1);

                // Character select
                Console.WriteLine("Press the number for your desired character:");
                Console.WriteLine("1) Valentine McKee");
                Console.WriteLine("2) Earl Bassett");
                Console.WriteLine("3) Burt Gummer");
                Console.WriteLine("4) Grady Hoover");
                Console.WriteLine("0) Classic Mode (No Character)");
                ConsoleKeyInfo input;
                bool success = false;
                do {
                    input = Console.ReadKey(true);

                    // Confirm input is a digit
                    success = int.TryParse(input.KeyChar.ToString(), out characterSelected);
                    if(!success || characterSelected > 4) {
                        Console.WriteLine("Please select a valid character choice.");
                        success = false;
                    }
                } while(!success);

                // Must assign characters first so that subscriptions later have targets to cross-subscribe to
                switch(characterSelected) {
                    case 0: // No Character
                        character = new noCharacter();
                        break;
                    case 1: // Valentine McKee
                        character = new valentineMcKee();
                        break;
                    case 2: // Earl Bassett
                        character = new earlBassett();
                        break;
                    case 3: // Burt Gummer
                        character = new burtGummer();
                        break;
                    case 4: // Grady Hoover
                        character = new gradyHoover();
                        break;
                }

                // Set to AI control if the name begins with "(CPU)"
                if((name.Substring(0, 5).Contains("(CPU)"))) {
                    cpu = true;
                    writeName();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(" WILL BE AI CONTROLLED! ");
                    Console.ResetColor();
                    Console.Write("(Just kidding, this isn't implemented yet.) Press any key to continue...");
                    Console.ReadKey(true);
                }
            }

            public void writeName() {
                Console.BackgroundColor = myColor;
                Console.Write(name);
                Console.BackgroundColor = ConsoleColor.Black;
            }

            // -- Player's Character functions

            // Character setup - subscribe to events
            /* This must be done after the number of players has been determined, and BOTH players' characters *
                * have been created, so that they can cross-subscribe ability events as necessary per character.
                * Ergo changes made below must ALSO BE reflected in the Player Constructor switch above!! */
            public void characterSubscriptions(Player self, Player opponent) {
                // EVENTS ARE LISTS OF SUBSCRIBERS
                // SUBSCRIBERS ARE FUNCTIONS ELSEWHERE THAT SHOULD RUN
                // DELEGATES ARE PROXIES OF OTHER FUNCTIONS
                // eventToWatchFor += functionToDo;
                switch(characterSelected) {
                    case 0: // No Character
                            //self.character.menuOptions.Add(new menuItem("Fire", (() => self.character.fire(opponent))));
                        break;
                    case 1: // Valentine McKee
                        self.character.graboidDied += self.character.ability1; // Don't You GD Push Me
                        self.character.graboidHit += self.character.ability2; // Fuuuu YOU
                        break;
                    case 2: // Earl Bassett
                        self.character.menuOptions.Add(new menuItem("R/C Bigfoot Bomb", (() => self.character.abilityO1(opponent))));
                        self.character.menuOptions.Add(new menuItem("Seizmojigger", (() => self.character.abilityO2(opponent))));
                        break;
                    case 3: // Burt Gummer
                        opponent.character.graboidHit += self.character.ability1; // Extra Ammo
                        self.character.menuOptions.Add(new menuItem("Cluster Charge", (() => self.character.abilityO2(opponent))));
                        break;
                    case 4: // Grady Hoover
                        opponent.character.graboidDied += self.character.ability1; // WA-BAM!! 50 G's
                        self.character.menuOptions.Add(new menuItem("Spray Fire", (() => self.character.abilityO2(opponent))));
                        break;
                }

                // Load remaining common menu options for all characters
                character.menuOptions.Add(new menuItem("View your board", (() => self.showBoard('g')), false));
                character.menuOptions.Add(new menuItem("View your graboids", (() => self.showGraboids('h')), false));
                character.menuOptions.Add(new menuItem("View your profile", (() => self.character.profile(self)), false));
                character.menuOptions.Add(new menuItem("View opponent's graboids", (() => opponent.showGraboids()), false));
                character.menuOptions.Add(new menuItem("View opponent's profile", (() => opponent.character.profile(opponent)), false));
            }

            public bool options(Player opponent) {
                bool result = character.options(opponent);
                return result;
            }

            // -- Player's Board functions

            // Displays player's board; 'n' switch means 'show no graboids', pass 'g' for 'show graboids'
            public void showBoard(char with = 'n') {
                this.writeName();
                Console.WriteLine("'s board:\n");
                this.board.showAll(with);
            }

            // -- Player's Graboid functions

            // Displays player's graboids; 'n' switch means 'show no hp', pass 'h' for 'show hp'
            public void showGraboids(char hp = 'n') {
                Console.WriteLine(name + "'s live graboids:");
                foreach(Graboid g in graboids) {
                    if(hp == 'h') {
                        Console.WriteLine(g.displayName + " (" + g.hp + "/" + g.maxHp + " hp)");
                    } else {
                        Console.WriteLine(g.displayName + " (?/" + g.maxHp + " hp)");
                    }
                }
            }

            // Returns an int = number of live graboids
            public int graboidsAlive() {
                return graboids.Count();
            }

            // Damages a graboid and assesses outcome - also causes end of the game!
            public void dmgGraboid(string toDmg) {
                var damagedGraboid = graboids.Single(g => g.type == toDmg);
                if(debug) {
                    Console.Write("{0}'s ", name);
                }
                bool dead = damagedGraboid.takeHit();
                if(dead) {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("You killed the {0}!", damagedGraboid.displayName);
                    Console.ResetColor();
                    graboids.Remove(damagedGraboid);
                    if(graboids.Count() == 0) {
                        gameOver = true;
                        return;
                    }
                    // Prod character to broadcast a kill event
                    this.character.getKilled();
                }
            }
        }

        // -- Game classes & functions

        public static bool gameOver = false;

        // Places a graboid on a board; Takes a graboid, and a board
        public static void setCoords(Graboid graboid, Board toBoard) {
            toBoard.showAll('g');
            Console.WriteLine(
                "Now placing your " + graboid.displayName + ", which is {0} spaces long.\n"
              + "Enter coordinates for each space, one at a time, in the format X#. All coordinates must be "
              + "adjacent and linear.", graboid.maxHp);

            // -- Collect input from user

            int piece = 0;
            do {
                Console.Write("Piece #{0}: ", piece + 1);
                graboid.coordinates[piece] = getCoords();
                piece++;
            } while(piece < graboid.maxHp);

            // -- Validate

            int errors = 0;

            // Check whether any coords are repeated
            for(int compare = 0; compare < graboid.hp - 1; compare++) {
                // Define how many coords left in the array need to be checked
                int rest = graboid.hp - compare - 1;
                for(int i = 0; i < rest; i++) {
                    // Define the coords you're checking against
                    int against = graboid.hp - rest + i;
                    if(graboid.coordinates[compare].SequenceEqual(graboid.coordinates[against])) {
                        char a = (char) (graboid.coordinates[compare][0] + 65);
                        Console.WriteLine("{0}{1} is repeated!", a, graboid.coordinates[compare][1]);
                        errors++;
                        break;
                    }
                }
                if(errors > 0) {
                    break;
                }
            }

            // Check whether coords are vertical or horizontal (all same letter OR all same digit)
            bool horizontal = true;
            int h = graboid.coordinates[0][0];
            foreach(int[] c in graboid.coordinates) {
                if(c[0] != h) {
                    horizontal = false;
                    break;
                }
            }

            bool vertical = true;
            int v = graboid.coordinates[0][1];
            foreach(int[] c in graboid.coordinates) {
                if(c[1] != v) {
                    vertical = false;
                    break;
                }
            }

            if(!horizontal && !vertical) {
                Console.WriteLine("Coordinates are not linear!");
                errors++;
            }

            // Check whether coords are contiguous
            if(errors == 0) {
                bool contiguous = true;
                int[] contiguousArray = new int[graboid.hp];

                // Whichever linearity is false from the previous check, populate contiguousArray with those coordinates
                if(vertical) {
                    for(int i = 0; i < graboid.hp; i++) {
                        contiguousArray[i] = graboid.coordinates[i][0];
                    }
                } else if(horizontal) {
                    for(int i = 0; i < graboid.hp; i++) {
                        contiguousArray[i] = graboid.coordinates[i][1];
                    }
                }
                Array.Sort(contiguousArray);

                // Each coordinate should be one more than the last
                int contiguousCheck = contiguousArray[0];
                for(int i = 1; i < graboid.hp; i++) {
                    if(contiguousArray[i] == contiguousCheck + 1) {
                        contiguousCheck++;
                    } else {
                        contiguous = false;
                        break;
                    }
                }
                if(!contiguous) {
                    Console.WriteLine("Coordinates are not contiguous!");
                    errors++;
                }
            }

            // Check whether any coords are already occupied
            foreach(int[] c in graboid.coordinates) {
                string[] occupied = target(toBoard, c);
                if(occupied[1] != null) {
                    char a = (char) (c[0] + 65);
                    Console.WriteLine("{0}{1} is already occupied by a graboid! ({2})", a, c[1], occupied[1]);
                    errors++;
                    break;
                }
            }

            if(errors > 0) {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Please re-select coordinates.");
                Console.ResetColor();
                setCoords(graboid, toBoard);
                return;
            }

            // -- Finalize

            // Write validated, confirmed set of coordinates to given board's spaces
            foreach(int[] c in graboid.coordinates) {
                toBoard.grid[c[0], c[1]].graboidType = graboid.type;
            }

            // Confirm or start over; Display board first, which is why coords are written BEFORE confirmation
            ConsoleKeyInfo confirm;
            toBoard.showAll('g');
            do {
                // Prompt for confirmation
                Console.WriteLine("Is this placement for {0} OK? Press Y to confirm, N to redo. ", graboid.displayName);
                confirm = Console.ReadKey(true);
            } while(confirm.Key != ConsoleKey.Y && confirm.Key != ConsoleKey.N);

            if(confirm.Key == ConsoleKey.Y) {
                Console.Clear();
                return;
            } else if(confirm.Key == ConsoleKey.N) {
                // Blank out previous choices from the board
                foreach(int[] c in graboid.coordinates) {
                    toBoard.grid[c[0], c[1]].graboidType = null;
                }
                // Start over
                setCoords(graboid, toBoard);
                return;
            }
        }

        // Creates players
        public static Player[] makePlayers() {
            // Generate players
            Player[] players = new Player[gameModeNoPlayers];
            for(int i = 0; i < players.Length; i++) {
                ConsoleColor playerColor;
                if(i == 0) {
                    playerColor = ConsoleColor.Red;
                } else {
                    playerColor = ConsoleColor.Blue;
                }
                Console.BackgroundColor = playerColor;
                Console.Write("\nPlayer {0}", i + 1);
                Console.ResetColor();
                Console.Write(" - ");
                players[i] = new Player(playerColor);
            }

            // Subscribe to events as necessary, set up menus with targets
            for(int i = 0; i < players.Length; i++) {
                players[i].characterSubscriptions(players[i], players[(i + 1) % players.Length]);
            }

            // Place graboids
            Console.Clear();
            foreach(Player p in players) {
                foreach(Graboid g in p.graboids) {
                    Console.Write("\nSetting up ");
                    p.writeName();
                    Console.WriteLine("'s board...");
                    setCoords(g, p.board);
                }
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n");
                p.writeName();
                Console.Write("'s board setup complete... Thank You!\n\n\n");
                Console.ResetColor();
            }

            // Have CPU place graboids (Phase 3 - involves AI)
            /*foreach(Graboid g in p2.graboids) {
                setCoords(g, p2.board);
            }*/

            return players;
        }

        // Main Program - Start Game

        public static void Main() {
            // Set debug on or off, and announce if it's on as a warning -------------- DEBUG SWITCH
            debug = false;
            if(debug) {
                gameModeShowBoard = 'g';
                gameModeNoPlayers = 1;
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Magenta;
                Console.WriteLine(
                    "======================================= DEBUG MODE ENGAGED =====================================");
                Console.ResetColor();
            } else {
                gameModeShowBoard = 'n';
                gameModeNoPlayers = 2;
            }

            // Setup players & boards, place graboids
            Console.Title = "Seizmojigger";
            try {
                Console.SetBufferSize(125, 300);
            } catch(System.NotImplementedException e) {
                // Blow it off, it's just a Mac
            }
            Console.SetWindowSize(125, 40);
            Console.WriteLine(
                "    *** Please adjust your console window size to at least 125 cols wide x 50 lines high ***    ");
            /*
                "============THE TREMORS BOARD GAME THAT IS KIND OF LIKE A POPULAR NAVAL COMBAT GAME=============\n"
                "     ^    /XXXXX XXXXXX X XXXXX X\   /X /XXXXX\      X X /XXXX\ /XXXX\ XXXXXX XXXXX\            \n"
                "    /|    X/     X    ^ X   /X/ XX\ /XX X/   \X\  ^  X X^X     /X    ^ X  /\  X   ^X   ^        \n"
                "--M/ |   /\XXXX\-XXX / \X--/X/--X\XVX/X-X-^ / X -/ \ X/X X--\XX X\ \XX\XXXX \-XXXX< \ / \----W--\n"
                "      | /   V \X X  V   X /X/   X \X/ X X\ V /X X   \X X X    X X V  X X/     X V \\ V          \n"
                "       V  XXXXX/ XXXXXX X XXXXX X     X \XXXXX/ \XXXX/ X \XXXX/ \XXXX/ XXXXXX X    \\           \n"
                "====================================It's a whole new ballgame! It's a whole new GD ballgame!!===\n"
                */

            // Title - Tag Line
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("\n============THE TREMORS BOARD GAME THAT IS KIND OF LIKE A POPULAR NAVAL COMBAT GAME=============\n");

            // Title - Line 1
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("     ^    ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/XXXXX XXXXXX X XXXXX X\\   /X /XXXXX\\      X X /XXXX\\ /XXXX\\ XXXXXX XXXXX\\            \n");

            // Title - Line 2
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("    /|    ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X/     X    ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("^ ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X   /X/ XX\\ /XX X/   \\X");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("\\  ^  ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X X");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("^");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X     ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("/");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X    ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("^ ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X  ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("/\\  ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X   ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("^");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X   ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("^        \n");

            // Title - Line 3
            Console.Write("---/ |   /");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\\XXXX\\");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("-");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("XXX");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write(" / \\");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("--");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/X/");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("--");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X\\XVX/X");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("-");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("-^ / ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write(" -/ \\ ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("/");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X X");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("--");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\\XX X");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("\\ ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\\XX");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("\\");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("XXXX ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("\\-");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("XXXX< ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("\\ / \\----W--\n");

            // Title - Line 4
            Console.Write("      | /   V ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\\X X  ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("V   ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X /X/   X \\X/ X X\\ ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("V ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("/X X   ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("\\");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X X X    X X ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("V  ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X X");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("/     ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("X ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("V ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\\\\ ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("V          \n");

            // Title - Line 5
            Console.Write("       V  ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("XXXXX/ XXXXXX X XXXXX X     X \\XXXXX/ \\XXXX/ X \\XXXX/ \\XXXX/ XXXXXX X    \\\\           \n");

            // Title - Subtitle
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("====================================");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("It's a whole new ballgame! It's a whole new GD ballgame!!");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("===\n");

            // Disclaimers & Info
            //  "====================================It's a whole new ballgame! It's a whole new GD ballgame!!===\n"
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\n" +
                "Created by Alan Staney, copyright 2016, version 1.3.1.0. The TREMORS franchise, its characters,\n" +
                "the term \"graboids\", and other intellectual properties remain solely the property of Stampede\n" +
                "Entertainment. This game was created without their permission or knowledge, and was created for\n" +
                "the sole purpose of paying homage to their fictitious universe; no money is being made, nor any\n" +
                "creative infringement intended, and honestly I should be so lucky as to attract their attention.");

            // Abstract
            Console.ResetColor();
            Console.Write("\n" +
                "You're here in Perfection, NV to exterminate graboids - competitively! At $50,000 bounty a head,\n" +
                "everyone's racing to kill the most graboids. The rules are similar to the familiar unmentionable\n" +
                "naval combat board game by Milton Bradley which was bought out by Hasbro. Each player will place\n" +
                "five different graboids of varying sizes on a 10 x 10 grid, then take turns firing on each other\n" +
                "in an effort to nail some worms! The first player to eliminate all 5 of the opponent's graboids\n" +
                "wins. Unlike the board game, you can select among characters from the TREMORS movies to gain new\n" +
                "special moves, some passive (meaning \"always working\"), some active with limited ammo (meaning\n" +
                "\"you need to specifically choose them instead of regularly firing\"). Check profiles for details!\n\n");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(
                "CHARACTER OVERVIEW:\n" +
                "VALENTINE MCKEE is a \"revenge\" character, who deals more damage the more he gets hit.\n" +
                "EARL BASSETT is a \"calculating\" character, with low firepower, but good at finding graboids.\n" +
                "BURT GUMMER is a \"powerhouse\" character, who shoots until he misses and uses area bombs.\n" +
                "GRADY HOOVER is a \"reckless\" character, with intermittent bursts of fire that often miss.\n");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(
                "\n**If you want to play an old-fashioned Battleship game, select \"No character\" as your character.\n" +
                "BE AWARE that if your opponent selects a character, you will quickly be out-gunned, therefore we\n" +
                "suggest that both players should agree to select \"No character\" ahead of time in fairness.\n\n");

            // Initialize
            Console.ResetColor();
            Player[] players = makePlayers();

            // Turn loops
            while(!gameOver) {
                for(int turn = 0; turn < players.Length; turn++) {
                    bool done = false;
                    do {
                        // Display opponent's board with graboids hidden
                        players[(turn + 1) % players.Length].showBoard(gameModeShowBoard);
                        Console.Write("\n");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        players[turn].writeName();
                        Console.Write("'s turn.\n");
                        Console.ResetColor();
                        done = players[turn].options(players[(turn + 1) % players.Length]);

                        // If the player chose a non-combat/recon action, alert that the turn is NOT over
                        if(!done) {
                            Console.Write("\nPress any key to ");
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write("continue your turn, ");
                            players[turn].writeName();
                            Console.ResetColor();
                            Console.Write("...");
                            Console.ReadKey();
                            Console.Clear();
                        }
                        // If the turn is not done, repeat
                    } while(!done);

                    // Upon gameOver, immediately end program
                    if(gameOver) {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("\n\n");
                        players[turn].writeName();
                        Console.Write(" wins!! ");
                        int left = players[turn].graboidsAlive();
                        if(left == 1) {
                            Console.Write("With only one graboid still alive!\n");
                        } else if(left > 1) {
                            Console.Write("With {0} graboids still alive!\n", left);
                        } else {
                            Console.Write("Great job, buddy.\n");
                        }
                        Console.ResetColor();
                        players[turn].showBoard('g');
                        Console.WriteLine("Press any key to EXIT GAME...");
                        Console.ReadKey();
                        return;
                    }

                    // End of a turn
                    Console.WriteLine("\nTurn Over. Press any key to continue...");
                    Console.ReadKey();
                    Console.Clear();
                }
            }
        }
    }

}
