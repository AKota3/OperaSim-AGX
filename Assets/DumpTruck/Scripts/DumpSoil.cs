using System;
using UnityEngine;
using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Model;
using AGXUnity.Utils;
using Math = System.Math;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PWRISimulator
{
    /// <summary>
    /// �p�t�H�[�}���X�̂��߂ɁA���̃N���X���L�ڂ���Merge Zone�Ƃ����{�b�N�X�ɗ��q������ƁA���q���ꎞ�I�ɏ����đS�ē��ꂽ���q
    /// �̑��ʂ���\�ʂŉ�������B���ꂽ���q�̑��ʂɂ���ĕ\�ʂ̍������ς��BMerge Zone���t���Ă���ב䍄�̂��΂߂ƂȂ�悤
    /// �ɏ��~�����ƁA���������q�̑��ʂɑ΂��Č��̏o�����痱�q���Đ�������ďo��B
    /// </summary>
    /// <remarks>
    /// �y���\�ʂ��������邽�߂ɁA����Component�Ɠ���GameObject�Ɉȍ~�̂Q��Component���}������Ă���K�v������F
    /// 1. DumpSoil.obj�Ƃ���Mesh���ݒ肳��Ă���Mesh Filter
    /// 2. DumpSoilMat�Ƃ���Material�A�܂���DumpSoilShader���g������Material�A���ݒ肳��Ă���Mesh Renderer
    /// </remarks>
    [RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshRenderer))]
    public class DumpSoil : ScriptComponent
    {
        #region Inspector Properties

        [Header("Loading")]

        [Tooltip("�ב�Ƀ}�[�W���ꂽ�y���̑��ʂ����@���e�����邩�ǂ����i�ב�RigidBody�ƃW���C���g�Ōq����RigidBody�Ƃ��Ĉ����j")]
        public bool addSoilMassRigidBody = true;


        [Header("Unloading")]

        [Tooltip("��납�痱�q�������ł��邩�ǂ����BPlay���Ƀh�A�̃��b�N��Ԃɂ����True/False�ɒ�������͂���")]
        public bool spawnParticlesEnabled = true;

        [Range(0.1f, 1)]
        [Tooltip("SpawnZone�̕��̃X�P�[���A1.0��MergeZone���Ɠ���ƂȂ�")]
        public float spawnZoneWidthScale = 0.9f;

        [Range(0, 90)]
        [Tooltip("�Œ�̕��y�p�x�B�ב䏸�~�����̊p�x�𒴂�������y�@�\���L���ɂȂ邪�A���C�Ȃǂɂ���Ă��傫���p�x�̕K�v�̂��Ƃ�����B")]
        public float mininumDumpAngle = 10.0f;

        [Range(0.01f, 10.0f)]
        [Tooltip("���y�̑��x�̏���im/s�j")]
        public float maximumSoilSpeed = 2.0f;

        [Range(0, 1)]
        [Tooltip("�ב�̎��͂ƒ��ɓ����Ă���y���̊Ԃ̖��C�W���B���y���x���e������B")]
        public float frictionCoefficient = 0.4f;

        [Range(0, 100)]
        [Tooltip("SpawnZone�̏o���ɋl�܂������q������������AfrictionCoefficient�������鐔�l�i���y���x�������邽�߂Ɂj")]
        public float fullSpawnZoneFrictionScale = 3.0f;

        [Range(0, 1)]
        public float fullSpawnZoneMarginFactor = 0.2f;

        [Header("Unloading Push Force")]

        [Range(0, 10000)]
        public float pushForceMinSoilMass = 200.0f;

        [Range(0, 10000)]
        public float pushForceMaxSoilMass = 1000.0f;

        [Tooltip("���y���ɏo���ɐ����������q�Ɍ������̗͂������邩�ǂ���")]
        public bool particlesPushForceEnabled = true;

        [Range(0.1f, 10f)]
        public float particlesPushForceScale = 1.0f;

        [Tooltip("���y���Ƀh�A�̍��̂Ɍ������̗͂������邩�ǂ���")]
        public bool doorPushForceEnabled = true;

        [Range(0.1f, 10f)]
        public float doorPushForceScale = 1.0f;

        [Tooltip("���y���Ƀh�A�̍��̂Ɍ������̗͂������邩�ǂ���")]
        [ConditionalHide(nameof(doorPushForceEnabled), hideCompletely = true)]
        public RigidBody doorBody;
        

        [Header("Visuals")]

        [Tooltip("Scene�E�B���h�E��MergeZone��\�����邩")]
        public bool showMergeZone = true;

        [Tooltip("Scene�E�B���h�E��SpawnZone��\�����邩�iPlay���̂݁j")]
        public bool showSpawnZone = true;

        [Range(0, 2)]
        public float soilVisualSpeedScale = 1.0f;


        [Header("Overrides (auto-assigned on Play)")]

        public DeformableTerrain terrain;
        public RigidBody containerBody;

        [Header("Output")]

        [InspectorLabel("Enabled")]
        public bool showOutputInInspector = false;

        #endregion

        #region Properties

        // �ב�ƃ}�[�W�������q�̑��ʁB
        public double soilMass { get; private set; } = 0.0;

        // ���݂̕��y���x�B
        public double soilSpeed { get; private set; } = 0.0;

        public double soilHeight { get { return mergeZoneHorizontalArea != 0.0 ? soilVolume / mergeZoneHorizontalArea : 0.0; } }

        public double soilVolume { get { return nominalParticleData.density != 0.0 ? soilMass / nominalParticleData.density : 0.0; } }
        
        public float tiltAngle { get { return Mathf.Abs(Mathf.Asin(forwardDir.y)) * Mathf.Rad2Deg; } }

        Vector3 localForwardDir { get { return Vector3.forward; } }

        Vector3 forwardDir { get { return transform.TransformDirection(localForwardDir); } }

        double maxNumParticlesInSpawnZone { get { return nominalParticleData.area != 0.0 ? spawnZoneVerticalArea / spawnParticleData.area : 0.0; } }

        Vector3 mergeZoneOriginalSize { get { return transform.localScale; } }

        Vector3 mergeZoneCurrentSize { get { return new Vector3(mergeZoneOriginalSize.x, (float)soilHeight, mergeZoneOriginalSize.z); } }

        Vector3 mergeZoneOriginalLocalCenterUnscaled { get { return new Vector3(0, 0.5f, 0.5f); } }

        Vector3 mergeZoneCurrentLocalCenterUnscaled { get { return new Vector3(0, 0.5f * (float)soilHeight / transform.localScale.y, 0.5f); } }

        double mergeZoneHorizontalArea { get { return mergeZoneOriginalSize.x * mergeZoneOriginalSize.z; } }
        
        Bounds mergeZoneOriginalBoundsWorld { get { return MathUtil.TransformBounds(transform, mergeZoneOriginalBoundsLocal); } }

        double spawnZoneWidth { get { return mergeZoneOriginalSize.x * spawnZoneWidthScale; } }

        double spawnZoneHeight { get { return Math.Max(soilHeight, spawnParticleData.diameter); } }

        double spawnZoneVerticalArea { get { return spawnZoneWidth * spawnZoneHeight; } }

        #endregion

        #region Private Fields

        // AgxDynamics�̓�����Terrain�I�u�W�F�N�g�B
        agxTerrain.Terrain terrainNative;

        // Terrain�̍ő唼�a�̂��闱�q�ɑ΂��ė��q�f�[�^�iTerrain��getParticleNominalRadius()�AgetMaterial()����v�Z���ꂽ�j�B
        ParticleData nominalParticleData;

        // �ב䂩����y���闱�q�̃f�[�^�B
        ParticleData spawnParticleData;

        // �ב�ƃ}�[�W�������q�̑��v���ʂ��܂˂��鍄�́B�ב䍄�̂�Lock�R���X�g���C���g�Ōq�����Ă���B
        RigidBody soilMassBody;

        // MergeZone�̌��X�̐��@�iEditor�Őݒ肵���́j�B
        Bounds mergeZoneOriginalBoundsLocal = new Bounds();
        
        // ����GameObject�̃y�A�����g�ב䍄�̂ɑ΂��Č��X�̑��ΓI�Ȉʒu�A��]�B
        agx.AffineMatrix4x4 transformRelativeToContainerBody;

        // Merge��Spawn�̍X�V���K�v���ǂ����B
        bool needsUpdate = true;

        // Spawn���ŐV�ɍX�V���ꂽGameTime�̎����B
        double lastSpawnUpdateTime = 0.0;
        
        // �}�[�W����Ă��Ȃ��icanMerge=false�̂����ŁjMergeZone�ɓ����Ă���B
        int numUnmergedParticlesInMergeZone = 0;

        // ���݂̏��~�p�x����̕��y�ő呬�x�B
        double maxPotentialSoilSpeed = 0.0;
        
        // ParticleEmitter�����q�𐶐�����]�[�����`����{�b�N�X�B
        agxCollide.Box emitterBox;

        // ������AgxDynamics��ParticleEmitter�B
        agx.ParticleEmitter emitter;

        // ParticleEmitter���J�n���獡�܂Ő����������q�̐��B
        double emittedQuantity = 0.0;

        #endregion

        #region Public Methods
       
        public void EnableSpawnParticles()
        {
            spawnParticlesEnabled = true;
        }

        public void DisableSpawnParticles()
        {
            spawnParticlesEnabled = false;
        }

        #endregion

        #region Private Methods

        protected override bool Initialize()
        {
            // �����I��Component���擾�F

            if (terrain == null)
                terrain = FindObjectOfType<DeformableTerrain>();

            if (containerBody == null)
                containerBody = GetComponentInParent<RigidBody>();

            // �G���[�`�F�b�N�F

            if (terrain?.GetInitialized<DeformableTerrain>() == null)
                return false;

            if (containerBody?.GetInitialized<RigidBody>() == null)
                return false;

            if (Simulation.Instance?.GetInitialized<Simulation>() == null)
                return false;

            // �f�[�^�̏����F

            mergeZoneOriginalBoundsLocal = new Bounds(mergeZoneOriginalLocalCenterUnscaled, Vector3.one);
            transformRelativeToContainerBody = AgxUtil.GetRelativeAgxTransform(containerBody.transform, transform);
            terrainNative = terrain?.GetInitialized<DeformableTerrain>()?.Native;
            nominalParticleData = ParticleData.CreateFromTerrainProperties(terrain);
            spawnParticleData = nominalParticleData;
            
            if (!CreateSoilMassBody())
                return false;

            if (!CreateEmitter())
                return false;

            StartCoroutine(UpdateParticleDataCoroutine(4.0f));

            return base.Initialize();
        }
        
        bool CreateSoilMassBody()
        {
            if (!addSoilMassRigidBody)
                return true;

            // �_���v�y���̎��ʂ�����RigidBody���쐬�i�Փ˕s�\�j
            GameObject bodyObject = new GameObject(name + "_SoilMassBody", typeof(RigidBody));
            bool asChild = GetComponentInParent<ArticulatedRoot>() == null; // ArticulatedRoot�̎q�ɂ���Ɩ�肪�������邩��
            if (asChild)
            {
                bodyObject.transform.parent = gameObject.transform;
                bodyObject.transform.localPosition = new Vector3(0, 0, 0.5f);
                bodyObject.transform.localRotation = Quaternion.identity;
                bodyObject.transform.localScale = Vector3.one;
            }
            else
            {
                bodyObject.transform.position = transform.TransformPoint(
                    mergeZoneOriginalLocalCenterUnscaled.x, 
                    0, 
                    mergeZoneOriginalLocalCenterUnscaled.z);
                bodyObject.transform.rotation = transform.rotation;
            }

            // ���ʐݒ�̏�����
            soilMassBody = bodyObject.GetComponent<RigidBody>().GetInitialized<RigidBody>();
            MassProperties massProps = soilMassBody.MassProperties;
            massProps.Mass.UseDefault = false;
            massProps.CenterOfMassOffset.UseDefault = false;
            massProps.InertiaDiagonal.UseDefault = false;
            UpdateSoilMassBody(); // ���ꂩ��A�eUpdate�ɌĂяo���Ď��ʐݒ���X�V������
            
            // soilMassBody�Ɖב��RigidBody���q��Constraint���쐬
            GameObject constraintObject = Factory.Create(ConstraintType.LockJoint, Vector3.zero, Quaternion.identity,
                                                         soilMassBody, containerBody);
            constraintObject.name = name + "_SoilMassJoint";
            constraintObject.transform.parent = bodyObject.transform.parent;
            constraintObject.GetComponent<Constraint>().GetInitialized<Constraint>(); // �����������邽��
            return true;
        }

        /// <summary>
        /// ���݂̃_���v�y���̎��ʂɍ��킹�āA�_���v�y��������RigidBody�̎��ʐݒ�𒲐�����B
        /// </summary>
        void UpdateSoilMassBody()
        {
            if (soilMassBody == null)
                return;

            Vector3 size = mergeZoneCurrentSize;
            float mass = Mathf.Max((float)soilMass, 1f); // �����G���W���ɖ�肪�������Ȃ����߁A���ʂ��[���ɂȂ�Ȃ��悤��
            float inertiaMassFactor = mass / 12f;

            MassProperties massProps = soilMassBody.MassProperties;
            massProps.Mass.Value = mass;
            massProps.CenterOfMassOffset.Value = new Vector3(0, (float)(size.y * 0.5), 0);
            massProps.InertiaDiagonal.Value = new Vector3(
                inertiaMassFactor * (size.y * size.y + size.z * size.z),
                inertiaMassFactor * (size.x * size.x + size.z * size.z),
                inertiaMassFactor * (size.x * size.x + size.y * size.y));
        }

        /// <summary>
        /// AgxDynamics��ParticleEmitter�𐶐�����B����ɁAParticleEmitter�����q�𐶐�����]�[�����`����Box���쐬�B����
        /// Box�̍��́A��ŉב�̓y�����ʂ��ς��ƍ��킹�Ē��������(UpdateEmitterPositionAndSize�Ƃ������\�b�h����)�B
        /// </summary>
        /// <returns></returns>
        bool CreateEmitter()
        {
            // ���qEmitter���쐬
            var granularBodySystem = terrainNative.getSoilSimulationInterface().getGranularBodySystem();
            emitter = new agx.ParticleEmitter(granularBodySystem, agx.Emitter.Quantity.QUANTITY_COUNT);
            emitter.setRate(0);
            emitter.setMaximumEmittedQuantity(0);
            var distTable = new agx.ParticleEmitter.DistributionTable(agx.Emitter.Quantity.QUANTITY_COUNT);
            distTable.addModel(
                new agx.ParticleEmitter.DistributionModel(
                    spawnParticleData.radius,
                    terrain.Native.getMaterial(agxTerrain.Terrain.MaterialType.PARTICLE), 1));
            emitter.setDistributionTable(distTable);
            Simulation.Instance.GetInitialized<Simulation>().Native.add(emitter);

            // ���qEmitter�̃G���A���L�ڂ���Box Shape�i�Փ˕s�\�j���쐬���A�ב��RigidBody�ɒǉ��B�}�[�W�G���A�̌��ɒu���B
            emitterBox = new agxCollide.Box(0.1, 0.1, 0.1);
            agxCollide.Geometry geometry = new agxCollide.Geometry(emitterBox);
            geometry.setEnableCollisions(false);
            Simulation.Instance.Native.add(geometry);
            containerBody.Native.add(geometry);
            UpdateEmitterPositionAndSize();

            // Box��Emitter�ɒǉ�
            granularBodySystem.setEnableCollisions(geometry, false);
            emitter.setGeometry(geometry);

            return true;
        }

        /// <summary>
        /// ���y���ɁA�����������q�܂��̓h�A�ɂ�����͂��v�Z�B�ב�̓y�����ʂ���щב�̏��~�p�x�ɂ���ĕς��B
        /// </summary>
        /// <returns></returns>
        double CalcPushForce()
        {
            double potentialMaxForce =
                9.81 * Mathf.Clamp((float)soilMass, pushForceMinSoilMass, pushForceMaxSoilMass) /
                maxNumParticlesInSpawnZone;
            return Math.Sin(tiltAngle * Mathf.Deg2Rad) * potentialMaxForce;
        }
        
        /// <summary>
        /// Unity���eFrame�Ɉ��Ăяo�����\�b�h�B
        /// </summary>
        void Update()
        {
            if (needsUpdate)
            {
                needsUpdate = false;

                UpdateMerge();
                UpdateSpawn();
                UpdateSoilMassBody();
            }
            UpdateVisualMaterial(Time.deltaTime);
        }

        /// <summary>
        /// ���y�̂��߁A�h�A�ɗ͂�������B
        /// </summary>
        void UpdateDoorForce()
        {
            if (!doorPushForceEnabled || doorBody?.GetInitialized<RigidBody>() == null)
                return;

            if (soilSpeed == 0.0 || numUnmergedParticlesInMergeZone == 0 || soilMass == 0)
                return;

            Vector3 forcePos = transform.position;
            Vector3 forceVec = doorPushForceScale * (float)-CalcPushForce() * 
                Vector3.ProjectOnPlane(forwardDir, Vector3.up).normalized;

            doorBody.Native.addForceAtPosition(forceVec.ToHandedVec3(), forcePos.ToHandedVec3());
        }

        /// <summary>
        /// AgxUnity���e�V�~�����[�V�����X�e�b�v�̌�ɌĂяo�����\�b�h�B���̃N���X��OnEnable()����R�[���o�b�N�Ƃ��ēo�^�����B
        /// </summary>
        void OnPostStepForward()
        {
            needsUpdate = true;

            UpdateDoorForce();
        }

        /// <summary>
        /// ���̃X�N���v�g��Enable�ɂȂ�Ƃ�Unity���Ăяo�����\�b�h�B
        /// </summary>
        protected override void OnEnable()
        {
            if (Simulation.HasInstance)
                Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;
            base.OnEnable();
        }

        /// <summary>
        /// ���̃X�N���v�g��Disable�ɂȂ�Ƃ�Unity���Ăяo�����\�b�h�B
        /// </summary>
        protected override void OnDisable()
        {
            if (Simulation.HasInstance)
                Simulation.Instance.StepCallbacks.PostStepForward -= OnPostStepForward;
            base.OnDisable();
        }

        /// <summary>
        /// MergeZone�ɓ����Ă��闱�q�����m���āA�K���ȍs�����s���F
        /// * ���y���s���Ă��Ȃ��ꍇ�́A���q�������Ď��ʂ��ב�y�ʂɒǉ�����B�܂�A�ב�y���ƃ}�[�W����B
        /// * ���y���s���Ă���ꍇ�́A�}�[�W���Ȃ��āA�ב�̌������ɗ��q�ɗ͂�������B
        /// </summary>
        void UpdateMerge()
        {
            agx.AffineMatrix4x4 inverseShapeTransform = new agx.AffineMatrix4x4(
                transform.rotation.ToHandedQuat(),
                transform.position.ToHandedVec3()).inverse();

            // ���[���h�o�E���f�B���O�{�b�N�X���v�Z�i���݂̓y�������𖳎�����j
            agx.Vec3 aabbMin, aabbMax;
            AgxUtil.ToAgxMinMax(mergeZoneOriginalBoundsWorld, out aabbMin, out aabbMax);
            aabbMin -= new agx.Vec3(nominalParticleData.radius);
            aabbMax += new agx.Vec3(nominalParticleData.radius);

            // ���[�J���o�E���f�B���O�{�b�N�X���v�Z�i���݂̓y��������Y���̃T�C�Y��ݒ�j
            agx.Vec3 localAABBMin = new agx.Vec3(-mergeZoneCurrentSize.x * 0.5, 0, 0);
            agx.Vec3 localAABBMax = new agx.Vec3(mergeZoneCurrentSize.x * 0.5, soilHeight, mergeZoneCurrentSize.z);

            bool canMerge = (soilSpeed == 0.0 && tiltAngle < mininumDumpAngle) || !spawnParticlesEnabled; // ���y���Ă��Ȃ��Ƃ������Ƀ}�[�W������
            double maxPotentialSoilSpeedSqrd = maxPotentialSoilSpeed * maxPotentialSoilSpeed; // ���y�̓y���̍ő呬�x
            agx.Vec3 pushForce = forwardDir.ToHandedVec3() * -CalcPushForce() * particlesPushForceScale;
            numUnmergedParticlesInMergeZone = 0; // Merge Zone�ɓ����Ă��邯�ǃ}�[�W���Ȃ����q�̐�

            // �S�Ă̗��q���擾
            var soilSimulation = terrainNative.getSoilSimulationInterface();
            var granulars = soilSimulation.getSoilParticles();
            int granularsCount = (int)granulars.size();

            // �e���q�𔽕�
            for (int i = 0; i < granularsCount; ++i)
            {
                var granule = granulars.at((uint)i);

                // Check 1: Check if center is inside axis-aligned world space bounding box (expanded by particle radius)
                agx.Vec3 pos = granule.position();
                if (pos.x > aabbMax.x || pos.x < aabbMin.x ||
                    pos.z > aabbMax.z || pos.z < aabbMin.z ||
                    pos.y > aabbMax.y || pos.y < aabbMin.y)
                {
                    granule.ReturnToPool();
                    continue;
                }

                // Check 2: Convert pos to local shape coordinates, and check if inside local axis-aligned bounding box
                agx.Vec3 localPos = inverseShapeTransform.transformPoint(pos);
                double radius = granule.getRadius();
                if (localPos.x - radius > localAABBMax.x || localPos.x + radius < localAABBMin.x ||
                    localPos.z - radius > localAABBMax.z || localPos.z + radius < localAABBMin.z ||
                    localPos.y - radius > localAABBMax.y || localPos.y + radius < localAABBMin.y)
                {
                    granule.ReturnToPool();
                    continue;
                }

                if (canMerge) // ���y���Ă��Ȃ���
                {
                    // ���q���ב�y���Ƀ}�[�W�A�܂藱�q�������ב�y���ʂ��X�V
                    soilMass += granule.getMass();
                    soilSimulation.removeSoilParticle(granule);
                }
                else  // ���y��
                {
                    // ����ɁA�l�܂�����m���邽�߂ɗ��q�𐔂���B
                    numUnmergedParticlesInMergeZone += 1;

                    // ���y����MergeZone�ɓ����Ă��܂����q�Ɍ������ɗ͂�������B
                    if (particlesPushForceEnabled &&
                        granule.getVelocity().length2() <= maxPotentialSoilSpeedSqrd)
                    {
                            granule.setForce(granule.getForce() + pushForce);
                    }
                }

                // Return the proxy class to the pool to avoid garbage.
                granule.ReturnToPool();
            }
        }
        
        /// <summary>
        /// �ב�̌��̗��qEmitter�̐������A���x���X�V�B����ɁA�����������q�ʂɍ��킹�ĉב�y���̗ʂ��X�V�B
        /// </summary>
        void UpdateSpawn()
        {
            float timeSinceLastUpdate = (float)(Time.timeAsDouble - lastSpawnUpdateTime);
            lastSpawnUpdateTime = Time.timeAsDouble;

            // �O���Update���琶�����ꂽ���q�̎��ʂ��ב䎿�ʂ������
            double emittedQuantityPrev = emittedQuantity;
            emittedQuantity = emitter.getEmittedQuantity();
            double deltaEmittedQuantity = emittedQuantity - emittedQuantityPrev;
            soilMass -= deltaEmittedQuantity * spawnParticleData.mass;

            // �����琶�����闱�q�̎��ʂȂǂ��o����
            spawnParticleData = nominalParticleData;

            // �ב�y���ʂ��[���A�܂��͉ב�p�x��������菬�����ꍇ�͗��q�������Ƃ߂�
            bool canSpawn = spawnParticlesEnabled && 
                            soilMass > 0.0 && 
                            tiltAngle >= mininumDumpAngle;
            if (canSpawn)
            {
                // �p�x����̉����x���v�Z
                float gravityAcc = 9.81f * Mathf.Sin(tiltAngle * Mathf.Deg2Rad);
                float frictionAcc = 9.81f * Mathf.Cos(tiltAngle * Mathf.Deg2Rad) * frictionCoefficient;

                // Spawn Zone�����q�ŋl�܂��Ă���ꍇ�́Agravity�����������Afriction��傫������
                float particlesInSpawnZoneRatio = numUnmergedParticlesInMergeZone / (float)maxNumParticlesInSpawnZone;
                if (particlesInSpawnZoneRatio > 1.0f)
                {
                    float effect = fullSpawnZoneMarginFactor > 0 ?
                        Mathf.Clamp01((particlesInSpawnZoneRatio - 1f) / fullSpawnZoneMarginFactor) : 1f;
                    frictionAcc *= fullSpawnZoneFrictionScale * (1f + effect); // effect��1.0�ɂȂ��frictionAcc�������ɃX�P�[��
                    gravityAcc *= 1f - effect; // effect��1.0�ɂȂ��gravityAcc��0.0�ɂȂ�
                }

                // �����x�ő��x���X�V�B �l�K�e�B�u�ɂȂ�Ȃ��悤�Ɋm�F
                soilSpeed += (gravityAcc - frictionAcc) * timeSinceLastUpdate;
                soilSpeed = Math.Max(soilSpeed, 0);

                // �p�x�ɂ���čő呬�x�ɐ���
                maxPotentialSoilSpeed = Mathf.Sin(tiltAngle * Mathf.Deg2Rad) * maximumSoilSpeed;
                soilSpeed = Math.Min(soilSpeed, maxPotentialSoilSpeed);
            }
            else
            {
                maxPotentialSoilSpeed = 0.0;
                soilSpeed = 0.0;
            }

            // ���qEmitter�̐������A���q�������x�A���q������𒲐�
            double flowVolume = soilSpeed * spawnZoneVerticalArea;
            double flowParticles = spawnParticleData.volume != 0.0 ? flowVolume / spawnParticleData.volume : 0.0;
            agx.Vec3 initParticleVelocity = soilSpeed * emitterBox.getGeometry().getFrame().transformVectorToLocal(-forwardDir.ToHandedVec3());
            double numSpawnableParticles = spawnParticleData.mass != 0.0 ? soilMass / spawnParticleData.mass : 0.0;
            double maximimuEmittedQuantity = emitter.getEmittedQuantity() + numSpawnableParticles;

            // AGX Emitter���؂�グ��悤�Ȃ���
            maximimuEmittedQuantity = Math.Max(0.0, Math.Floor(maximimuEmittedQuantity));

            emitter.setRate(flowParticles);
            emitter.setVelocity(initParticleVelocity);
            emitter.setMaximumEmittedQuantity(maximimuEmittedQuantity);

            // ���qEmitter�̍������ב�ɓ����Ă���y���̗ʂɍ��킹�čX�V
            UpdateEmitterPositionAndSize();
        }

        /// <summary>
        /// �ב�ɓ����Ă���y���̗ʂɍ��킹�ė��qEmitter�̍����𒲐��B
        /// </summary>
        void UpdateEmitterPositionAndSize()
        {
            if (emitterBox == null)
                return;

            emitterBox.setHalfExtents(new agx.Vec3(
                0.5 * spawnZoneWidth,
                0.5 * spawnZoneHeight,
                0.5 * spawnParticleData.diameter));

            agx.AffineMatrix4x4 relativeToMergeZone = agx.AffineMatrix4x4.translate(
                0,  
                emitterBox.getHalfExtents().y,
                emitterBox.getHalfExtents().z);

            emitterBox.getGeometry().setLocalTransform(
                relativeToMergeZone * transformRelativeToContainerBody);
        }
        
        /// <summary>
        /// AGXUnity��Terrain��ParticleMaterial���ύX���ꂽ�̂����m���A�ύX���ꂽ�ꍇ�͊֌W�̂��闱�q�f�[�^�����킹�čX�V����B
        /// </summary>
        /// <param name="updateInterval">ParticleMaterial���`�F�b�N�������(�b)</param>
        System.Collections.IEnumerator UpdateParticleDataCoroutine(float updateInterval)
        {
            if (terrain?.GetInitialized<DeformableTerrain>()?.Native == null)
                yield break;

            double? previousDensity = null;
            while (true)
            {
                yield return new WaitForSeconds(updateInterval);

                double density = terrain.Native.getMaterial(
                    agxTerrain.Terrain.MaterialType.PARTICLE).getBulkMaterial().getDensity();

                if (previousDensity != null && previousDensity != density)
                {
                    nominalParticleData = ParticleData.CreateFromTerrainProperties(terrain);
                    Debug.Log($"{name} : Detected a change in Terrain particle material density parameter. " +
                              $"Updating internal particle data cache. {nominalParticleData}.");
                    
                }
                previousDensity = density;
            }
        }

        #endregion

        #region Visuals

        // �r�W���A���p�̃R���|�[�l���g�AMaterial�v���p�e�B
        MeshRenderer meshRenderer;
        MaterialPropertyBlock materialPropertyBlock;
        double soilVisualMovedDistance = 0.0;

        /// <summary>
        /// �y�ʁA���y���x�Ȃǂɍ��킹�āA�y���\�ʃ��b�V���̃����_�����O�}�e���A���̃p�����[�^���X�V����B
        /// </summary>
        /// <param name="deltaTime">�O��ɌĂяo�����Ƃ����炩������Game����</param>
        void UpdateVisualMaterial(double deltaTime)
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            if (materialPropertyBlock == null)
                materialPropertyBlock = new MaterialPropertyBlock();

            soilVisualMovedDistance += soilSpeed * deltaTime * soilVisualSpeedScale;

            float visualSoilHeight = (float)soilHeight;

            bool zeroHeightWhenOneParticleOrLess = true;
            if (zeroHeightWhenOneParticleOrLess)
            {
                float oneParticleSoilHeight = (float)(nominalParticleData.mass / (nominalParticleData.density * mergeZoneHorizontalArea));
                float invLerp = Mathf.InverseLerp(oneParticleSoilHeight, 10.0f, visualSoilHeight);
                visualSoilHeight = Mathf.LerpUnclamped(0, 10.0f, invLerp);
            }

            materialPropertyBlock.SetFloat("_SoilSlideOffset", (float)soilVisualMovedDistance / transform.localScale.z);
            materialPropertyBlock.SetFloat("_SoilBaseHeight", visualSoilHeight / transform.localScale.y);
            materialPropertyBlock.SetFloat("_SoilHeightMapMaxHeight", Mathf.Lerp(0.0f, 1.0f, Mathf.Sqrt(visualSoilHeight * 2.0f)));
            materialPropertyBlock.SetFloat("_TiltAngle", tiltAngle);

            meshRenderer.SetPropertyBlock(materialPropertyBlock);
        }

        /// <summary>
        /// �f�o�b�M���O���邽�߂ɁAMerge Zone�ASpawn Zone��Scene�E�B���h�E���ɕ\������B
        /// </summary>
        void OnDrawGizmos()
        {
            Matrix4x4 prevMatrix = Gizmos.matrix;
            try
            {
                if (showMergeZone)
                {
                    // Box�̈ʒu�A��]�A�X�P�[����ݒ�
                    Gizmos.matrix = transform.localToWorldMatrix;

                    // Play���̏ꍇ�́ABox�̍��������݂̓y�������ɍ��킹�Ē���
                    Vector3 localScale = Application.isPlaying ?
                        new Vector3(1, Mathf.Max(0.001f, (float)soilHeight / transform.localScale.y), 1) :
                        Vector3.one;

                    Vector3 localPos = Application.isPlaying ?
                        mergeZoneCurrentLocalCenterUnscaled :
                        mergeZoneOriginalLocalCenterUnscaled;

                    // Box�̕\�ʂ�\��
                    Gizmos.color = new Color(0.1f, 1.0f, 0.1f, 0.2f);
                    Gizmos.DrawCube(localPos, localScale);

                    // Box�̃G�b�W��\��
                    Gizmos.color = Gizmos.color * 2.0f;
                    Gizmos.DrawWireCube(localPos, localScale);
                }

                if(showSpawnZone)
                {
                    if (Application.isPlaying && emitterBox != null)
                    {
                        // Box�̈ʒu�A��]�A�X�P�[����ݒ�
                        Gizmos.matrix = Matrix4x4.TRS(emitterBox.getGeometry().getPosition().ToHandedVector3(),
                                                      emitterBox.getGeometry().getRotation().ToHandedQuaternion(),
                                                      Vector3.one);
                        
                        Vector3 size = emitterBox.getHalfExtents().ToVector3() * 2.0f;

                        // Box�̕\�ʂ�\��
                        Gizmos.color = new Color(1.0f, 0.1f, 0.1f, 0.2f);
                        Gizmos.DrawCube(Vector3.zero, size);

                        // Box�̃G�b�W��\��
                        Gizmos.color = Gizmos.color * 2.0f;
                        Gizmos.DrawWireCube(Vector3.zero, size);
                    }
                }
            }
            finally { Gizmos.matrix = prevMatrix; }
        }

#endregion
    }

    /// <summary>
    /// Terrain���q�̃v���p�e�B��ۑ�����X�g���N�`���[�B
    /// </summary>
    struct ParticleData
    {
        public double radius { get; private set; }
        public double diameter { get; private set; }
        public double area { get; private set; }
        public double volume { get; private set; }
        public double mass { get; private set; }
        public double density { get; private set; }

        public ParticleData(double radius, double density)
        {
            this.radius = radius;
            this.density = density;
            diameter = 2.0 * radius;
            area = radius * radius * Math.PI;
            volume = Math.Pow(radius, 3.0) * Math.PI * 4.0 / 3.0;
            mass = density * volume;
        }

        public static ParticleData CreateFromTerrainProperties(DeformableTerrain terrain)
        {
            return new ParticleData(
                terrain.Native.getParticleNominalRadius(),
                terrain.Native.getMaterial(agxTerrain.Terrain.MaterialType.PARTICLE).getBulkMaterial().getDensity());
        }

        static public double CalcMass(double radius, double density)
        {
            return density * Math.Pow(radius, 3.0) * Math.PI * 4.0 / 3.0;
        }

        static public double CalcRadius(double mass, double density)
        {
            return Math.Pow(mass * 3.0 / (Math.PI * 4.0 * density), 1.0 / 3.0);
        }

        public override string ToString()
        {
            return $"radius = {radius: 0.####}, diameter = {diameter: 0.####}, area = {area: 0.####}, " +
                   $"volume = {volume: 0.####}, mass = {mass: 0.####}, density = {density : 0.####}";
        }
    };

#if UNITY_EDITOR
    [CustomEditor(typeof(DumpSoil))]
    class DumpSoilEditor : Editor
    {
        public override bool RequiresConstantRepaint()
        {
            return RequiresConstantRepaint((DumpSoil)target);
        }

        static public bool RequiresConstantRepaint(DumpSoil dump)
        {
            return dump.showOutputInInspector && (dump.soilMass > 0.0 || dump.soilSpeed > 0.0);
        }

        public override void OnInspectorGUI()
        {
            // �W����GUI��\��
            base.OnInspectorGUI();

            var data = (DumpSoil)target;

            if (data.showOutputInInspector)
                OnSoilDataGUI(data);
        }

        static public void OnSoilDataGUI(DumpSoil data)
        {
            EditorGUILayout.LabelField("Soil mass:", $"{data.soilMass: 0.###} kg");
            EditorGUILayout.LabelField("Soil height:", $"{data.soilHeight: 0.###} m");
            EditorGUILayout.LabelField("Soil volume:", $"{data.soilVolume: 0.###} m3");
        }
    }
#endif
}
