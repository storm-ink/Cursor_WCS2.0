<template>
  <div class="monitor3d-wrapper">
    <div class="monitor3d-container" ref="containerRef"></div>
    <div class="monitor3d-overlay">
      <div class="overlay-item">
        <span class="overlay-dot" :class="cvConnected ? 'on' : ''"></span>
        CV01 输送线
      </div>
      <div class="overlay-item">
        <span class="overlay-dot" :class="craneConnected ? 'on' : ''"></span>
        CR01 堆垛机
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import * as THREE from 'three'
import { useSignalR } from '../stores/signalr'

const containerRef = ref(null)
const cvConnected = ref(false)
const craneConnected = ref(false)
const { joinGroup, leaveGroup, on, off } = useSignalR()

let scene, camera, renderer, animationId
let shelves = []
let crane = null
let craneTarget = { x: 0, y: 0, z: 0 }

function init() {
  const container = containerRef.value
  if (!container) return

  scene = new THREE.Scene()
  scene.background = new THREE.Color(0x060d1b)
  scene.fog = new THREE.FogExp2(0x060d1b, 0.007)

  camera = new THREE.PerspectiveCamera(55, container.clientWidth / container.clientHeight, 0.1, 1000)
  camera.position.set(30, 25, 35)
  camera.lookAt(0, 0, 0)

  renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true })
  renderer.setSize(container.clientWidth, container.clientHeight)
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
  renderer.shadowMap.enabled = true
  renderer.shadowMap.type = THREE.PCFSoftShadowMap
  container.appendChild(renderer.domElement)

  scene.add(new THREE.AmbientLight(0x4488cc, 0.5))
  const dirLight = new THREE.DirectionalLight(0xffffff, 0.7)
  dirLight.position.set(20, 30, 20)
  dirLight.castShadow = true
  scene.add(dirLight)
  scene.add(new THREE.PointLight(0x3b9eff, 0.8, 60))

  createFloor()
  createShelves()
  createConveyor()
  createCrane()
  createLabels()

  window.addEventListener('resize', onResize)

  let angle = 0
  function animate() {
    animationId = requestAnimationFrame(animate)
    angle += 0.002
    camera.position.x = 38 * Math.cos(angle)
    camera.position.z = 38 * Math.sin(angle)
    camera.lookAt(0, 2, 0)

    if (crane) {
      crane.position.x += (craneTarget.x - crane.position.x) * 0.02
      crane.position.y += (craneTarget.y - crane.position.y) * 0.02
      crane.position.z += (craneTarget.z - crane.position.z) * 0.02
    }
    renderer.render(scene, camera)
  }
  animate()
}

function createFloor() {
  scene.add(new THREE.GridHelper(60, 60, 0x142240, 0x0b1529))
  const floor = new THREE.Mesh(
    new THREE.PlaneGeometry(60, 60),
    new THREE.MeshStandardMaterial({ color: 0x060d1b, transparent: true, opacity: 0.9 })
  )
  floor.rotation.x = -Math.PI / 2
  floor.position.y = -0.01
  floor.receiveShadow = true
  scene.add(floor)
}

function createShelves() {
  const shelfMat = new THREE.MeshStandardMaterial({ color: 0x1a3a6a, transparent: true, opacity: 0.35 })
  const palletMat = new THREE.MeshStandardMaterial({ color: 0xf59e0b, transparent: true, opacity: 0.65 })
  const shelfGeo = new THREE.BoxGeometry(1.8, 0.8, 1.2)
  const palletGeo = new THREE.BoxGeometry(1.4, 0.6, 0.9)

  for (let row = 0; row < 2; row++) {
    for (let col = 0; col < 10; col++) {
      for (let level = 0; level < 5; level++) {
        const shelf = new THREE.Mesh(shelfGeo, shelfMat.clone())
        shelf.position.set(-12 + col * 2.2, 0.5 + level * 1.0, -8 + row * 18)
        shelf.castShadow = true
        scene.add(shelf)
        shelves.push(shelf)

        if (Math.random() > 0.4) {
          const pallet = new THREE.Mesh(palletGeo, palletMat.clone())
          pallet.position.copy(shelf.position)
          pallet.position.y += 0.1
          scene.add(pallet)
        }
      }
    }
  }
}

function createConveyor() {
  const group = new THREE.Group()
  const rollerMat = new THREE.MeshStandardMaterial({ color: 0x546e7a })
  const railMat = new THREE.MeshStandardMaterial({ color: 0x3b9eff, emissive: 0x1a3a6a })

  for (let i = 0; i < 8; i++) {
    const roller = new THREE.Mesh(new THREE.CylinderGeometry(0.15, 0.15, 2, 8), rollerMat)
    roller.rotation.z = Math.PI / 2
    roller.position.set(-12 + i * 3, 0.3, 2)
    group.add(roller)
  }
  const railGeo = new THREE.BoxGeometry(24, 0.08, 0.08)
  const r1 = new THREE.Mesh(railGeo, railMat); r1.position.set(0, 0.3, 1); group.add(r1)
  const r2 = new THREE.Mesh(railGeo, railMat); r2.position.set(0, 0.3, 3); group.add(r2)
  scene.add(group)
}

function createCrane() {
  const group = new THREE.Group()
  const mast = new THREE.Mesh(new THREE.BoxGeometry(0.3, 12, 0.3), new THREE.MeshStandardMaterial({ color: 0xef4444, emissive: 0x7f1d1d }))
  mast.position.y = 6; group.add(mast)
  const arm = new THREE.Mesh(new THREE.BoxGeometry(4, 0.2, 0.3), new THREE.MeshStandardMaterial({ color: 0xf59e0b }))
  arm.position.set(0, 3, 0); group.add(arm)
  const fork = new THREE.Mesh(new THREE.BoxGeometry(0.8, 0.1, 1.5), new THREE.MeshStandardMaterial({ color: 0x22c55e, emissive: 0x166534 }))
  fork.position.set(1.5, 3, 0); group.add(fork)
  const base = new THREE.Mesh(new THREE.BoxGeometry(2, 0.3, 1.5), new THREE.MeshStandardMaterial({ color: 0x37474f }))
  base.position.y = 0.15; group.add(base)
  group.position.set(15, 0, -2)
  scene.add(group)
  crane = group
}

function makeLabel(text, color) {
  const c = document.createElement('canvas')
  c.width = 256; c.height = 64
  const ctx = c.getContext('2d')
  ctx.fillStyle = color
  ctx.font = 'bold 22px Arial'
  ctx.fillText(text, 10, 38)
  const tex = new THREE.CanvasTexture(c)
  const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex }))
  s.scale.set(4, 1, 1)
  return s
}

function createLabels() {
  const l1 = makeLabel('CV01 输送线', '#3b9eff')
  l1.position.set(0, 2, 2); scene.add(l1)
  const l2 = makeLabel('CR01 堆垛机', '#ef4444')
  l2.position.set(15, 14, -2); scene.add(l2)
}

function onResize() {
  const c = containerRef.value
  if (!c || !camera || !renderer) return
  camera.aspect = c.clientWidth / c.clientHeight
  camera.updateProjectionMatrix()
  renderer.setSize(c.clientWidth, c.clientHeight)
}

function handleDeviceStatus(data) {
  if (!Array.isArray(data)) return
  const cv = data.find(d => d.type === 'Conveyor')
  const cr = data.find(d => d.type === 'Crane')
  if (cv) cvConnected.value = cv.isConnected
  if (cr) craneConnected.value = cr.isConnected
}

onMounted(() => {
  init()
  joinGroup('view:realtime')
  on('DeviceStatusUpdated', handleDeviceStatus)
})

onUnmounted(() => {
  cancelAnimationFrame(animationId)
  leaveGroup('view:realtime')
  off('DeviceStatusUpdated', handleDeviceStatus)
  if (renderer) { renderer.dispose(); containerRef.value?.removeChild(renderer.domElement) }
  window.removeEventListener('resize', onResize)
})
</script>

<style scoped>
.monitor3d-wrapper {
  position: relative;
  width: 100%;
  height: calc(100vh - 84px);
  border-radius: 10px;
  overflow: hidden;
  border: 1px solid var(--border);
}
.monitor3d-container { width: 100%; height: 100%; }

.monitor3d-overlay {
  position: absolute;
  top: 16px; right: 16px;
  display: flex; flex-direction: column; gap: 8px;
}
.overlay-item {
  background: rgba(6, 13, 27, 0.85);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 6px 14px;
  font-size: 11px;
  color: var(--text-secondary);
  display: flex; align-items: center; gap: 8px;
  backdrop-filter: blur(8px);
}
.overlay-dot {
  width: 7px; height: 7px; border-radius: 50%;
  background: var(--danger);
  flex-shrink: 0;
}
.overlay-dot.on {
  background: var(--success);
  box-shadow: 0 0 6px rgba(34, 197, 94, 0.5);
}
</style>
