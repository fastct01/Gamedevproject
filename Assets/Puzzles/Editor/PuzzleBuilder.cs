using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Puzzles > Build ... : places the two puzzles (greybox shapes) on Alien Ground 2 and 3.
/// Re-running a build replaces that puzzle. Everything is placed on the ground found by raycast.
/// </summary>
public static class PuzzleBuilder
{
    const string MatDir = "Assets/Puzzles/Materials";
    static readonly Vector2 Ground2Center = new Vector2(8.75f, 12.98f);   // MassiveAliendGround (2), west of the ship
    static readonly Vector2 Ground3Center = new Vector2(16.06f, 20.54f);  // MassiveAliendGround (3), north of the ship

    static Transform _root;
    static Vector2 _c;

    // ======================================================================
    // Puzzle 2 - The Weighing Gate (Alien Ground 3)
    // ======================================================================
    [MenuItem("Tools/Puzzles/Build Weighing Gate (Alien Ground 3)")]
    public static void BuildWeighingGate()
    {
        if (!Begin("Puzzle_WeighingGate", Ground3Center)) return;
        var stone = Mat("Stone", new Color(0.36f, 0.36f, 0.4f));
        var metal = Mat("GateMetal", new Color(0.55f, 0.58f, 0.62f), smooth: 0.6f, metallic: 0.6f);
        var plateMat = Mat("Plate", new Color(0.25f, 0.27f, 0.3f));
        var dot = Mat("GlyphDot", new Color(0.8f, 0.95f, 1f), emission: new Color(0.6f, 0.9f, 1f) * 1.2f);
        var crate = Mat("WeightCube", new Color(0.62f, 0.5f, 0.38f));
        var egg = Mat("AlienEgg", new Color(0.7f, 0.85f, 0.6f), smooth: 0.7f);
        var tank = Mat("OxygenTank", new Color(0.2f, 0.5f, 1f), emission: new Color(0.1f, 0.35f, 1f));

        const float wallH = 0.6f, t = 0.08f;

        // Walled compound at the north end with a sliding gate on its south side.
        Box("Compound Back", P(0f, 2.9f), wallH, new Vector3(1.28f, wallH, t), stone);
        Box("Compound Left", P(-0.6f, 2.3f), wallH, new Vector3(t, wallH, 1.2f), stone);
        Box("Compound Right", P(0.6f, 2.3f), wallH, new Vector3(t, wallH, 1.2f), stone);
        Box("Compound Front L", P(-0.43f, 1.7f), wallH, new Vector3(0.42f, wallH, t), stone);
        Box("Compound Front R", P(0.43f, 1.7f), wallH, new Vector3(0.42f, wallH, t), stone);
        var gatePanel = Box("Gate", P(0f, 1.77f), wallH, new Vector3(0.45f, wallH, 0.05f), metal);
        var gate = gatePanel.AddComponent<PuzzleGate>();
        gate.openOffset = new Vector3(0.46f, 0f, 0f);
        gate.duration = 2f;

        // Reward: oxygen cache inside (switched on when solved).
        var reward = new GameObject("Reward (on solve)");
        Place(reward.transform, P(0f, 2.3f), 0f);
        var zoneGo = new GameObject("Oxygen Zone (Cache North)");
        Place(zoneGo.transform, P(0f, 2.3f), 0f);
        zoneGo.transform.SetParent(reward.transform, true);
        var zone = zoneGo.AddComponent<OxygenZone>();
        zone.radius = 0.4f;
        var lightGo = new GameObject("Cache Light", typeof(Light));
        lightGo.transform.position = P(0f, 2.3f) + Vector3.up * 0.3f;
        lightGo.transform.SetParent(reward.transform, true);
        var l = lightGo.GetComponent<Light>();
        l.type = LightType.Point; l.range = 0.8f; l.intensity = 0.15f; l.color = new Color(0.4f, 0.7f, 1f);
        // Oxygen tanks are always visible through the gate, as the goal.
        Cyl("Oxygen Tank A", P(0.3f, 2.65f), 0.16f, 0.06f, tank, true);
        Cyl("Oxygen Tank B", P(0.42f, 2.6f), 0.13f, 0.05f, tank, true);
        reward.SetActive(false);

        // The blaster waits on a pedestal inside the cache.
        InventoryBuilder.AddBlasterPickup(_root, P(-0.3f, 2.45f));

        // Pressure plates: heavy (3 dots), medium (2 dots), light on a raised pillar (1 dot).
        var heavy = Plate("Plate Heavy (3 dots)", P(-1.0f, 0.9f), 0f, 0.42f, 10f, 40f, 3, plateMat, dot);
        var medium = Plate("Plate Medium (2 dots)", P(0f, 0.9f), 0f, 0.3f, 1.2f, 5f, 2, plateMat, dot);
        const float pillarH = 0.3f;
        Box("Light Plate Pillar", P(1.0f, 0.9f), pillarH, new Vector3(0.36f, pillarH, 0.36f), stone);
        // Low rim on top so thrown things stay on.
        float rimH = 0.05f, top = pillarH;
        Box("Rim N", P(1.0f, 1.07f), rimH, new Vector3(0.36f, rimH, 0.02f), stone, yBase: top);
        Box("Rim S", P(1.0f, 0.73f), rimH, new Vector3(0.36f, rimH, 0.02f), stone, yBase: top);
        Box("Rim E", P(1.17f, 0.9f), rimH, new Vector3(0.02f, rimH, 0.36f), stone, yBase: top);
        Box("Rim W", P(0.83f, 0.9f), rimH, new Vector3(0.02f, rimH, 0.36f), stone, yBase: top);
        var light = Plate("Plate Light (1 dot)", P(1.0f, 0.9f), top, 0.3f, 0.2f, 1.1f, 1, plateMat, dot);

        var group = _root.gameObject.AddComponent<PlateGroup>();
        group.plates = new[] { heavy, medium, light };
        group.gate = gate;
        group.activateOnSolve = new[] { reward };

        // Weights to work with: same-density cubes (each half the size) + a 1 kg alien egg.
        WeightCube("Weight Cube Large (20 kg)", P(-2.6f, -2.6f), 0.25f, 20f, false, crate);
        WeightCube("Weight Cube Medium (2.5 kg)", P(2.4f, -1.8f), 0.125f, 2.5f, false, crate);
        WeightCube("Weight Cube Small (0.31 kg)", P(-0.6f, -3.0f), 0.0625f, 0.3125f, true, crate);
        var eggGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eggGo.name = "Alien Egg (1 kg)";
        eggGo.transform.localScale = Vector3.one * 0.08f;
        Place(eggGo.transform, P(2.7f, 0.2f) + Vector3.up * 0.04f, 0f);
        eggGo.GetComponent<Renderer>().sharedMaterial = egg;
        var erb = eggGo.AddComponent<Rigidbody>();
        erb.mass = 1f; erb.angularDrag = 2f; erb.drag = 0.5f; erb.interpolation = RigidbodyInterpolation.Interpolate;
        eggGo.AddComponent<Grabbable>();
        eggGo.AddComponent<Pushable>();

        End("Weighing Gate built on Alien Ground 3.");
    }

    // ======================================================================
    // Puzzle 1 - The Lightwell (Alien Ground 2)
    // ======================================================================
    [MenuItem("Tools/Puzzles/Build Lightwell (Alien Ground 2)")]
    public static void BuildLightwell() => BuildLightwell(simple: true);

    [MenuItem("Tools/Puzzles/Build Lightwell - Advanced Version (Alien Ground 2)")]
    public static void BuildLightwellAdvanced() => BuildLightwell(simple: false);

    // Simple: core -> cradle, two mirrors on two pads (turn each once), one white receiver. Core never runs out.
    // Advanced: adds a splitter, blue filter + second receiver, a walled pocket you throw a mirror into,
    //           two decoy sockets, a slow heavy mirror, and a core that drains after 90 s.
    static void BuildLightwell(bool simple)
    {
        if (!Begin("Puzzle_Lightwell", Ground2Center)) return;
        var stone = Mat("Stone", new Color(0.36f, 0.36f, 0.4f));
        var emitterMat = Mat("Emitter", new Color(0.3f, 0.32f, 0.36f), smooth: 0.6f, metallic: 0.5f);
        var pad = Mat("SocketPad", new Color(0.3f, 0.55f, 0.6f), emission: new Color(0.1f, 0.35f, 0.4f));
        var mirrorHeavy = Mat("MirrorHeavy", new Color(0.35f, 0.45f, 0.6f), smooth: 0.95f, metallic: 0.9f);
        var mirrorLight = Mat("MirrorLight", new Color(0.55f, 0.6f, 0.68f), smooth: 0.6f, metallic: 0.5f);
        var mirrorFace = Mat("MirrorFace", new Color(0.92f, 0.97f, 1f), emission: new Color(0.25f, 0.4f, 0.5f), smooth: 1f, metallic: 0.9f);
        var splitterMat = Mat("Splitter", new Color(0.5f, 1f, 0.95f, 0.55f), emission: new Color(0.1f, 0.4f, 0.4f), transparent: true);
        var filterMat = Mat("BlueFilter", new Color(0.15f, 0.35f, 1f, 0.5f), emission: new Color(0.05f, 0.15f, 0.6f), transparent: true);
        var receiverMat = Mat("Receiver", new Color(0.15f, 0.15f, 0.18f));
        var coreMat = Mat("PowerCore", new Color(1f, 0.55f, 0.15f), emission: new Color(1f, 0.55f, 0.15f) * 2f);
        var glyphMat = Mat("DarkGlyph", new Color(0.4f, 1f, 0.7f, 0.9f), unlit: true, transparent: true);
        var whiteBeam = Mat("BeamWhite", new Color(2f, 2f, 1.9f), unlit: true);
        var blueBeam = Mat("BeamBlue", new Color(0.3f, 0.6f, 2.5f), unlit: true);

        float g0 = GroundY(_c.x, _c.y);
        float beamY = g0 + 0.06f;
        const float wallH = 0.35f;

        // --- Emitter + core cradle (east side, near the way in from the ship) ---
        var emitter = Box("Emitter", P(3.0f, -1.2f), 0.2f, new Vector3(0.14f, 0.2f, 0.14f), emitterMat);
        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetPositionAndRotation(new Vector3(_c.x + 3.0f - 0.08f, beamY, _c.y - 1.2f), Quaternion.Euler(0f, -90f, 0f));
        muzzle.transform.SetParent(emitter.transform, true);
        var cradle = Socket("Core Cradle", P(3.0f, -0.8f), 0f, "Core", false, pad, PadShape.Circle);
        Box("Cradle Cable", P(3.0f, -1.0f), 0.01f, new Vector3(0.02f, 0.01f, 0.32f), emitterMat, collider: false);
        var em = emitter.AddComponent<LightEmitter>();
        em.muzzle = muzzle.transform;
        em.cradle = cradle;
        em.whiteBeam = whiteBeam;
        em.blueBeam = blueBeam;
        if (simple) em.drainSeconds = 0f; // never runs out

        // Power core, half buried near the south rocks.
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Power Core";
        core.transform.localScale = Vector3.one * 0.06f;
        Place(core.transform, (simple ? P(1.6f, -2.4f) : P(0.6f, -2.9f)) + Vector3.up * 0.02f, 0f);
        core.GetComponent<Renderer>().sharedMaterial = coreMat;
        var crb = core.AddComponent<Rigidbody>();
        crb.mass = 1f; crb.angularDrag = 2f; crb.drag = 0.5f; crb.interpolation = RigidbodyInterpolation.Interpolate;
        core.AddComponent<Grabbable>();
        core.AddComponent<SnapItem>().kind = "Core";
        var pc = core.AddComponent<PowerCore>();
        pc.glowRenderer = core.GetComponent<Renderer>();
        var coreLight = new GameObject("Core Glow", typeof(Light));
        coreLight.transform.SetParent(core.transform, false);
        var cl = coreLight.GetComponent<Light>();
        cl.type = LightType.Point; cl.range = 0.25f; cl.color = new Color(1f, 0.6f, 0.2f);
        pc.glowLight = cl;

        // --- Rock wall that blocks the straight shot ---
        Box("Rock Wall", P(-0.5f, -1.2f), wallH, new Vector3(0.15f, wallH, 2.8f), stone);

        // --- Mirror sockets ---
        Socket("Socket S2", P(1.5f, -1.2f), 0f, "Mirror", true, pad);
        Socket("Socket S4", P(1.5f, 1.6f), 90f, "Mirror", true, pad);
        if (!simple)
        {
            Socket("Socket S1", P(-1.8f, -0.2f), 0f, "Mirror", true, pad);
            Socket("Socket S5", P(3.0f, 1.6f), 0f, "Mirror", true, pad);
            var s3 = Socket("Socket S3 (walled pocket)", P(1.5f, 3.0f), 135f, "Mirror", false, pad);
            s3.snapRadius = 0.2f; // anywhere inside the pocket
        }

        // Walled pocket around S3: too tall to climb, with slots at beam height (south in, east out).
        float slotLo = beamY - 0.025f, slotHi = beamY + 0.025f;
        if (!simple)
        {
        PocketWall("Pocket S", new Vector3(_c.x + 1.5f, 0, _c.y + 2.775f), new Vector3(0.45f, 0, 0.05f), true, slotLo, slotHi, wallH, stone);
        PocketWall("Pocket E", new Vector3(_c.x + 1.725f, 0, _c.y + 3.0f), new Vector3(0.05f, 0, 0.45f), false, slotLo, slotHi, wallH, stone);
        Box("Pocket N", P(1.5f, 3.225f), wallH, new Vector3(0.5f, wallH, 0.05f), stone);
        Box("Pocket W", P(1.275f, 3.0f), wallH, new Vector3(0.05f, wallH, 0.45f), stone);
        }

        // --- Filter + receivers ---
        BeamReceiver[] receivers;
        if (simple)
        {
            receivers = new[] { Receiver("Receiver", P(-3.2f, 1.6f), 90f, BeamColor.White, Color.white, receiverMat, emitterMat) };
        }
        else
        {
            var filter = Box("Blue Filter Crystal", P(-0.5f, 1.6f), 0.18f, new Vector3(0.04f, 0.18f, 0.16f), filterMat);
            filter.AddComponent<BeamFilter>().color = BeamColor.Blue;
            var r2 = Receiver("Receiver R2 (blue)", P(-3.2f, 1.6f), 90f, BeamColor.Blue, new Color(0.3f, 0.55f, 1f), receiverMat, emitterMat);
            var r1 = Receiver("Receiver R1 (white)", P(3.2f, 3.0f), -90f, BeamColor.White, Color.white, receiverMat, emitterMat);
            receivers = new[] { r1, r2 };
        }

        // --- Movable mirrors ---
        if (simple)
        {
            // Two easy-to-carry mirrors, each lying near the pad it belongs on.
            // Spawn angles chosen so each needs exactly one press of R (A: 0 -> 45, B: 180 -> 225).
            Mirror("Mirror A", P(2.2f, -2.2f), 1f, false, true, mirrorLight, mirrorFace, 0f);
            Mirror("Mirror B", P(2.4f, 2.3f), 1f, false, true, mirrorLight, mirrorFace, 180f);
        }
        else
        {
            Mirror("Mirror (heavy, 20 kg)", P(-2.0f, -2.5f), 20f, false, false, mirrorHeavy, mirrorFace, 0f);
            Mirror("Splitter Crystal (2.5 kg)", P(-2.4f, 3.2f), 2.5f, true, false, splitterMat, null, 0f); // flat: passes light through
            Mirror("Mirror (light, throwable)", P(-1.2f, 3.5f), 0.3125f, false, true, mirrorLight, mirrorFace, 0f);
        }

        // --- Glow-in-the-dark hints (only visible with the flashlight off): arrows show where the beam should go next ---
        Glyph("Hint S2 (beam goes north)", P(1.5f + 0.16f, -1.2f), 0f, glyphMat);
        Glyph("Hint S4 (beam goes west)", P(1.5f, 1.6f - 0.16f), -90f, glyphMat);
        if (!simple) Glyph("Hint Pocket (beam goes east)", P(1.5f, 2.62f), 90f, glyphMat);
        Glyph("Hint Cradle", P(3.0f - 0.14f, -0.8f), 180f, glyphMat);

        // --- The Lightwell (south-west) and its reward ---
        Vector3 wellPos = P(-3.0f, -2.2f);
        float rh = 0.06f;
        Box("Well Rim N", wellPos + new Vector3(0, 0, 0.2f), rh, new Vector3(0.44f, rh, 0.04f), stone);
        Box("Well Rim S", wellPos + new Vector3(0, 0, -0.2f), rh, new Vector3(0.44f, rh, 0.04f), stone);
        Box("Well Rim E", wellPos + new Vector3(0.2f, 0, 0), rh, new Vector3(0.04f, rh, 0.36f), stone);
        Box("Well Rim W", wellPos + new Vector3(-0.2f, 0, 0), rh, new Vector3(0.04f, rh, 0.36f), stone);
        var lidGo = Box("Well Lid", wellPos, 0.03f, new Vector3(0.38f, 0.03f, 0.38f), emitterMat, yBase: rh);
        var lid = lidGo.AddComponent<PuzzleGate>();
        lid.openOffset = new Vector3(0f, 0.04f, -0.42f);
        lid.duration = 2.5f;

        var reward = new GameObject("Reward (on solve)");
        Place(reward.transform, wellPos, 0f);
        var zoneGo = new GameObject("Oxygen Zone (Lightwell)");
        Place(zoneGo.transform, wellPos, 0f);
        zoneGo.transform.SetParent(reward.transform, true);
        zoneGo.AddComponent<OxygenZone>().radius = 0.45f;
        // The ship part (gear) sits in the opened well.
        InventoryBuilder.AddGearPickup(reward.transform, wellPos);
        reward.SetActive(false);

        var well = _root.gameObject.AddComponent<Lightwell>();
        well.receivers = receivers;
        well.lid = lid;
        well.activateOnSolve = new[] { reward };

        End(simple ? "Lightwell (simple) built on Alien Ground 2." : "Lightwell (advanced) built on Alien Ground 2.");
    }

    [MenuItem("Tools/Puzzles/Set Up Player For Puzzles")]
    public static void SetUpPlayer()
    {
        var interactor = Object.FindFirstObjectByType<ObjectInteractor>();
        if (interactor == null) { Debug.LogWarning("No ObjectInteractor in the scene."); return; }
        if (interactor.GetComponent<MirrorRotateInput>() == null) Undo.AddComponent<MirrorRotateInput>(interactor.gameObject);
        Undo.RecordObject(interactor, "Puzzle throw speed");
        interactor.throwSpeed = 3f; // the world is tiny: 3 units/s throws about one unit, enough to land on a ledge
        EditorSceneManager.MarkSceneDirty(interactor.gameObject.scene);
    }

    // ======================================================================
    // helpers
    // ======================================================================
    static bool Begin(string name, Vector2 center)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode before building puzzles."); return false; }
        var old = GameObject.Find(name);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build " + name);
        _c = center;
        go.transform.position = new Vector3(center.x, GroundY(center.x, center.y), center.y);
        _root = go.transform;
        return true;
    }

    static void End(string msg)
    {
        SetUpPlayer();
        Selection.activeGameObject = _root.gameObject;
        EditorSceneManager.MarkSceneDirty(_root.gameObject.scene);
        Debug.Log(msg);
    }

    /// <summary>World point on the ground at an offset from the puzzle centre.</summary>
    static Vector3 P(float dx, float dz)
    {
        float x = _c.x + dx, z = _c.y + dz;
        return new Vector3(x, GroundY(x, z), z);
    }

    static float GroundY(float x, float z)
    {
        float best = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 10f, z), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.attachedRigidbody != null) continue;
            if (!h.collider.transform.root.name.StartsWith("MassiveAliendGround")) continue; // ground only
            best = Mathf.Max(best, h.point.y);
        }
        return float.IsNegativeInfinity(best) ? 0.937f : best;
    }

    static void Place(Transform t, Vector3 pos, float yaw)
    {
        t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        t.SetParent(_root, true);
    }

    /// <summary>Box sitting on the ground (or on yBase above it) at a ground point.</summary>
    static GameObject Box(string name, Vector3 groundPoint, float height, Vector3 size, Material m, bool collider = true, float yBase = 0f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.localScale = size;
        Place(go.transform, groundPoint + Vector3.up * (yBase + height * 0.5f), 0f);
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Cyl(string name, Vector3 groundPoint, float height, float diameter, Material m, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
        Place(go.transform, groundPoint + Vector3.up * height * 0.5f, 0f);
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static PressurePlate Plate(string name, Vector3 groundPoint, float yBase, float size, float min, float max, int dots, Material m, Material dotMat)
    {
        var go = new GameObject(name);
        Place(go.transform, groundPoint + Vector3.up * yBase, 0f);
        // Trigger volume above the plate (the plate itself has no collider so boxes slide on without catching).
        var trig = go.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.center = new Vector3(0f, 0.15f, 0f);
        trig.size = new Vector3(size, 0.3f, size);
        var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "Plate Top";
        Object.DestroyImmediate(top.GetComponent<Collider>());
        top.transform.SetParent(go.transform, false);
        top.transform.localPosition = new Vector3(0f, 0.005f, 0f);
        top.transform.localScale = new Vector3(size, 0.01f, size);
        top.GetComponent<Renderer>().sharedMaterial = m;
        for (int i = 0; i < dots; i++)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            d.name = "Weight Glyph Dot";
            Object.DestroyImmediate(d.GetComponent<Collider>());
            d.transform.SetParent(go.transform, false);
            d.transform.localPosition = new Vector3((i - (dots - 1) * 0.5f) * 0.06f, 0.012f, -size * 0.5f + 0.04f);
            d.transform.localScale = new Vector3(0.035f, 0.002f, 0.035f);
            d.GetComponent<Renderer>().sharedMaterial = dotMat;
        }
        var plate = go.AddComponent<PressurePlate>();
        plate.minMass = min;
        plate.maxMass = max;
        plate.indicator = top.GetComponent<Renderer>();
        return plate;
    }

    static void WeightCube(string name, Vector3 groundPoint, float size, float mass, bool grabbable, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.localScale = Vector3.one * size;
        Place(go.transform, groundPoint + Vector3.up * (size * 0.5f + 0.001f), 0f);
        go.GetComponent<Renderer>().sharedMaterial = m;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        go.AddComponent<Pushable>();
        if (grabbable) go.AddComponent<Grabbable>();
    }

    enum PadShape { Mirror, Circle }

    // Mirror pads are a rectangle matching the triangle mirror's footprint (0.12 wide, 0.06 deep behind the
    // mirror face) and turn with the mirror. The core cradle is a round pad.
    static SnapSocket Socket(string name, Vector3 groundPoint, float yaw, string accepts, bool rotatable, Material padMat, PadShape shape = PadShape.Mirror)
    {
        var go = new GameObject(name);
        Place(go.transform, groundPoint, 0f);
        var pivot = new GameObject("Pad Pivot").transform;
        pivot.SetParent(go.transform, false);
        pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var padGo = GameObject.CreatePrimitive(shape == PadShape.Circle ? PrimitiveType.Cylinder : PrimitiveType.Cube);
        padGo.name = "Pad";
        Object.DestroyImmediate(padGo.GetComponent<Collider>());
        padGo.transform.SetParent(pivot, false);
        if (shape == PadShape.Circle)
        {
            padGo.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            padGo.transform.localScale = new Vector3(0.12f, 0.002f, 0.12f);
        }
        else
        {
            padGo.transform.localPosition = new Vector3(0f, 0.002f, -0.03f);
            padGo.transform.localScale = new Vector3(0.14f, 0.004f, 0.08f);
        }
        padGo.GetComponent<Renderer>().sharedMaterial = padMat;
        var s = go.AddComponent<SnapSocket>();
        s.accepts = accepts;
        s.rotatable = rotatable;
        s.yaw = yaw;
        s.snapRadius = 0.1f;
        s.padVisual = pivot;
        return s;
    }

    // faceMat != null: triangular prism, only the long (hypotenuse) face is the mirror, drawn with faceMat.
    // faceMat == null: flat double-sided slab (used for the splitter, which lets light through).
    static void Mirror(string name, Vector3 groundPoint, float mass, bool splitter, bool grabbable, Material m, Material faceMat, float yaw)
    {
        GameObject go;
        if (faceMat != null)
        {
            go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            var mesh = PrismMesh();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { m, faceMat };
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
            Place(go.transform, groundPoint + Vector3.up * 0.061f, yaw);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = new Vector3(0.12f, 0.12f, 0.03f);
            Place(go.transform, groundPoint + Vector3.up * 0.061f, yaw);
            go.GetComponent<Renderer>().sharedMaterial = m;
        }
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // stays upright
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        go.AddComponent<BeamMirror>().splitter = splitter;
        go.AddComponent<Pushable>();
        go.AddComponent<SnapItem>().kind = "Mirror";
        if (grabbable) go.AddComponent<Grabbable>();
    }

    // Upright right-angled triangular prism, 0.12 wide x 0.12 tall x 0.06 deep.
    // The long face points along +Z (forward) and passes through the pivot, so a mirror on a pad reflects
    // exactly at the pad centre and bounced beams stay lined up with the next pad.
    // Submesh 1 = the mirror face, submesh 0 = the rest.
    static Mesh PrismMesh()
    {
        const string path = "Assets/Puzzles/Materials/MirrorPrism_v2.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        const float hw = 0.06f, hh = 0.06f, hd = 0.03f;
        Vector3 A = new Vector3(-hw, 0, 0), B = new Vector3(hw, 0, 0), C = new Vector3(0, 0, -2f * hd);
        Vector3 up = Vector3.up * hh;
        var verts = new System.Collections.Generic.List<Vector3>();
        var body = new System.Collections.Generic.List<int>();
        var face = new System.Collections.Generic.List<int>();

        void Tri(System.Collections.Generic.List<int> list, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var t = b; b = c; c = t; }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            list.Add(i); list.Add(i + 1); list.Add(i + 2);
        }
        void Quad(System.Collections.Generic.List<int> list, Vector3 p, Vector3 q, Vector3 outward)
        {
            // vertical quad from bottom edge p-q up to the top
            Tri(list, p - up, q - up, q + up, outward);
            Tri(list, p - up, q + up, p + up, outward);
        }

        Quad(face, A, B, Vector3.forward);                                  // mirror face
        Quad(body, A, C, Vector3.Cross(Vector3.up, C - A).normalized);      // left back (points -X -Z)
        Quad(body, C, B, Vector3.Cross(Vector3.up, B - C).normalized);      // right back (points +X -Z)
        Tri(body, A + up, B + up, C + up, Vector3.up);                      // top
        Tri(body, A - up, B - up, C - up, Vector3.down);                    // bottom

        var mesh = new Mesh { name = "MirrorPrism" };
        mesh.SetVertices(verts);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(body, 0);
        mesh.SetTriangles(face, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Puzzles", "Materials");
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // A dish on a post with a lens in the middle, facing the incoming beam (yaw: 0 = faces +Z, 90 = faces +X).
    // The lens lights up when hit by the right colour.
    static BeamReceiver Receiver(string name, Vector3 groundPoint, float yaw, BeamColor color, Color lit, Material lensMat, Material metal)
    {
        const float centreY = 0.06f; // beam height above the ground
        var go = new GameObject(name);
        Place(go.transform, groundPoint, yaw);
        var col = go.AddComponent<BoxCollider>(); // what the beam hits
        col.center = new Vector3(0f, centreY, 0f);
        col.size = new Vector3(0.13f, 0.13f, 0.04f);

        GameObject Part(PrimitiveType type, string partName, Vector3 lp, Vector3 euler, Vector3 scale, Material m)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = partName;
            Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(go.transform, false);
            p.transform.localPosition = lp;
            p.transform.localRotation = Quaternion.Euler(euler);
            p.transform.localScale = scale;
            p.GetComponent<Renderer>().sharedMaterial = m;
            return p;
        }

        Part(PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.004f, -0.025f), Vector3.zero, new Vector3(0.09f, 0.004f, 0.09f), metal);
        Part(PrimitiveType.Cylinder, "Post", new Vector3(0f, centreY * 0.5f, -0.025f), Vector3.zero, new Vector3(0.018f, centreY * 0.5f, 0.018f), metal);
        // Dish: a flat disc facing forward, with a thicker rim behind it.
        Part(PrimitiveType.Cylinder, "Dish Rim", new Vector3(0f, centreY, -0.012f), new Vector3(90f, 0f, 0f), new Vector3(0.13f, 0.008f, 0.13f), metal);
        Part(PrimitiveType.Cylinder, "Dish", new Vector3(0f, centreY, -0.004f), new Vector3(90f, 0f, 0f), new Vector3(0.11f, 0.004f, 0.11f), metal);
        var lens = Part(PrimitiveType.Sphere, "Lens", new Vector3(0f, centreY, 0.004f), Vector3.zero, new Vector3(0.045f, 0.045f, 0.03f), lensMat);
        // Three small prongs around the lens, like an antenna feed.
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            Vector3 dir = Quaternion.Euler(0f, 0f, a) * Vector3.up;
            Part(PrimitiveType.Cube, "Prong", new Vector3(0f, centreY, 0f) + dir * 0.04f + Vector3.forward * 0.01f,
                new Vector3(0f, 0f, a), new Vector3(0.006f, 0.03f, 0.006f), metal);
        }

        var r = go.AddComponent<BeamReceiver>();
        r.required = color;
        r.litColor = lit;
        r.indicator = lens.GetComponent<Renderer>();
        var lg = new GameObject("Glow", typeof(Light));
        lg.transform.SetParent(go.transform, false);
        lg.transform.localPosition = new Vector3(0f, centreY, 0.05f);
        var l = lg.GetComponent<Light>();
        l.type = LightType.Point; l.range = 0.4f; l.color = lit; l.enabled = false;
        r.glow = l;
        return r;
    }

    // Wall with a beam-height slot in the middle. alongX: wall runs along X (slot centred on its X), else along Z.
    static void PocketWall(string name, Vector3 center, Vector3 footprint, bool alongX, float slotLo, float slotHi, float wallH, Material m)
    {
        float g = GroundY(center.x, center.z);
        float len = alongX ? footprint.x : footprint.z;
        float thick = alongX ? footprint.z : footprint.x;
        const float slotW = 0.08f;
        Vector3 Size(float l, float h) => alongX ? new Vector3(l, h, thick) : new Vector3(thick, h, l);
        Vector3 Along(float d) => alongX ? new Vector3(d, 0, 0) : new Vector3(0, 0, d);
        Vector3 basePt = new Vector3(center.x, g, center.z);

        // below and above the slot (full length)
        Box(name + " Low", basePt, slotLo - g, Size(len, slotLo - g), m);
        Box(name + " High", basePt, g + wallH - slotHi, Size(len, g + wallH - slotHi), m, yBase: slotHi - g);
        // either side of the slot, in the slot's height band
        float side = (len - slotW) * 0.5f;
        float off = slotW * 0.5f + side * 0.5f;
        Box(name + " SideA", basePt + Along(-off), slotHi - slotLo, Size(side, slotHi - slotLo), m, yBase: slotLo - g);
        Box(name + " SideB", basePt + Along(off), slotHi - slotLo, Size(side, slotHi - slotLo), m, yBase: slotLo - g);
    }

    // Flat arrow on the ground pointing along yaw (0 = +Z / north).
    static void Glyph(string name, Vector3 groundPoint, float yaw, Material m)
    {
        var go = new GameObject(name);
        Place(go.transform, groundPoint + Vector3.up * 0.003f, yaw);
        void Part(Vector3 lp, Vector3 s, float ry)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(go.transform, false);
            p.transform.localPosition = lp;
            p.transform.localRotation = Quaternion.Euler(0f, ry, 0f);
            p.transform.localScale = s;
            p.GetComponent<Renderer>().sharedMaterial = m;
            p.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Part(new Vector3(0, 0, 0), new Vector3(0.015f, 0.002f, 0.1f), 0f);
        Part(new Vector3(-0.018f, 0, 0.035f), new Vector3(0.012f, 0.002f, 0.05f), 35f);
        Part(new Vector3(0.018f, 0, 0.035f), new Vector3(0.012f, 0.002f, 0.05f), -35f);
        go.AddComponent<DarkGlyph>();
    }

    static Material Mat(string name, Color color, Color? emission = null, bool transparent = false, bool unlit = false, float smooth = 0.3f, float metallic = 0f)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Puzzles", "Materials");
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color);
        if (!unlit) { m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metallic); }
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
        if (transparent)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
