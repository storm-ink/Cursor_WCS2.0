<template>
  <div class="monitor3d-container" ref="containerRef"></div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import * as THREE from 'three'
import { useSignalR } from '../stores/signalr'

const containerRef = ref(null)
const { joinGroup, leaveGroup, on, off } = useSignalR()

let scene, camera, renderer, animationId
let shelves = []
let conveyor = null
let crane = null
let craneTarget = { x: 0, y: 0, z: 0 }

function init() {
  const container = containerRef.value
  if (!container) return

  scene = new THREE.Scene()
  scene.background = new THREE.Color(0x0a1628)
  scene.fog = new THREE.FogExp2(0x0a1628, 0.008)

  camera = new THREE.PerspectiveCamera(60, container.clientWidth / container.clientHeight, 0.1, 1000)
  camera.position.set(30, 25, 35)
  camera.lookAt(0, 0, 0)

  renderer = new THREE.WebGLRenderer({ antialias: true })
  renderer.setSize(container.clientWidth, container.clientHeight)
  renderer.setPixelRatio(window.devicePixelRatio)
  renderer.shadowMap.enabled = true
  container.appendChild(renderer.domElement)

  const ambientLight = new THREE.AmbientLight(0x4488cc, 0.6)
  scene.add(ambientLight)

  const dirLight = new THREE.DirectionalLight(0xffffff, 0.8)
  dirLight.position.set(20, 30, 20)
  dirLight.castShadow = true
  scene.add(dirLight)

  const pointLight = new THREE.PointLight(0x4fc3f7, 1, 60)
  pointLight.position.set(0, 15, 0)
  scene.add(pointLight)

  createFloor()
  createShelves()
  createConveyor()
  createCrane()
  createLabels()

  window.addEventListener('resize', onResize)

  let angle = 0
  function animate() {
    animationId = requestAnimationFrame(animate)

    angle += 0.003
    camera.position.x = 35 * Math.cos(angle)
    camera.position.z = 35 * Math.sin(angle)
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
  const gridHelper = new THREE.GridHelper(60, 60, 0x1e3a5f, 0x0d1f3c)
  scene.add(gridHelper)

  const floorGeo = new THREE.PlaneGeometry(60, 60)
  const floorMat = new THREE.MeshStandardMaterial({
    color: 0x0a1628,
    transparent: true,
    opacity: 0.8
  })
  const floor = new THREE.Mesh(floorGeo, floorMat)
  floor.rotation.x = -Math.PI / 2
  floor.position.y = -0.01
  floor.receiveShadow = true
  scene.add(floor)
}

function createShelves() {
  for (let row = 0; row < 2; row++) {
    for (let col = 0; col < 10; col++) {
      for (let level = 0; level < 5; level++) {
        const geo = new THREE.BoxGeometry(1.8, 0.8, 1.2)
        const mat = new THREE.MeshStandardMaterial({
          color: 0x1565c0,
          transparent: true,
          opacity: 0.4,
          wireframe: Math.random() > 0.5
        })
        const shelf = new THREE.Mesh(geo, mat)
        shelf.position.set(
          -12 + col * 2.2,
          0.5 + level * 1.0,
          -8 + row * 18
        )
        shelf.castShadow = true
        scene.add(shelf)
        shelves.push(shelf)

        if (Math.random() > 0.4) {
          const palletGeo = new THREE.BoxGeometry(1.4, 0.6, 0.9)
          const palletMat = new THREE.MeshStandardMaterial({
            color: 0xff9800,
            transparent: true,
            opacity: 0.7
          })
          const pallet = new THREE.Mesh(palletGeo, palletMat)
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

  for (let i = 0; i < 8; i++) {
    const rollerGeo = new THREE.CylinderGeometry(0.15, 0.15, 2, 8)
    const rollerMat = new THREE.MeshStandardMaterial({ color: 0x78909c })
    const roller = new THREE.Mesh(rollerGeo, rollerMat)
    roller.rotation.z = Math.PI / 2
    roller.position.set(-12 + i * 3, 0.3, 2)
    group.add(roller)
  }

  const railGeo = new THREE.BoxGeometry(24, 0.1, 0.1)
  const railMat = new THREE.MeshStandardMaterial({ color: 0x4fc3f7, emissive: 0x1565c0 })

  const rail1 = new THREE.Mesh(railGeo, railMat)
  rail1.position.set(0, 0.3, 1)
  group.add(rail1)

  const rail2 = new THREE.Mesh(railGeo, railMat)
  rail2.position.set(0, 0.3, 3)
  group.add(rail2)

  scene.add(group)
  conveyor = group
}

function createCrane() {
  const group = new THREE.Group()

  const mastGeo = new THREE.BoxGeometry(0.3, 12, 0.3)
  const mastMat = new THREE.MeshStandardMaterial({ color: 0xef5350, emissive: 0x8b0000 })
  const mast = new THREE.Mesh(mastGeo, mastMat)
  mast.position.y = 6
  group.add(mast)

  const armGeo = new THREE.BoxGeometry(4, 0.2, 0.3)
  const armMat = new THREE.MeshStandardMaterial({ color: 0xffa726 })
  const arm = new THREE.Mesh(armGeo, armMat)
  arm.position.set(0, 3, 0)
  group.add(arm)

  const forkGeo = new THREE.BoxGeometry(0.8, 0.1, 1.5)
  const forkMat = new THREE.MeshStandardMaterial({ color: 0x66bb6a, emissive: 0x2e7d32 })
  const fork = new THREE.Mesh(forkGeo, forkMat)
  fork.position.set(1.5, 3, 0)
  group.add(fork)

  const baseGeo = new THREE.BoxGeometry(2, 0.3, 1.5)
  const baseMat = new THREE.MeshStandardMaterial({ color: 0x455a64 })
  const base = new THREE.Mesh(baseGeo, baseMat)
  base.position.y = 0.15
  group.add(base)

  group.position.set(15, 0, -2)
  scene.add(group)
  crane = group
}

function createLabels() {
  const canvas = document.createElement('canvas')
  canvas.width = 256
  canvas.height = 64
  const ctx = canvas.getContext('2d')
  ctx.fillStyle = '#4fc3f7'
  ctx.font = 'bold 24px Arial'
  ctx.fillText('输送线 CV01', 10, 40)

  const texture = new THREE.CanvasTexture(canvas)
  const spriteMat = new THREE.SpriteMaterial({ map: texture })
  const sprite = new THREE.Sprite(spriteMat)
  sprite.position.set(0, 2, 2)
  sprite.scale.set(4, 1, 1)
  scene.add(sprite)

  const canvas2 = document.createElement('canvas')
  canvas2.width = 256
  canvas2.height = 64
  const ctx2 = canvas2.getContext('2d')
  ctx2.fillStyle = '#ef5350'
  ctx2.font = 'bold 24px Arial'
  ctx2.fillText('堆垛机 CR01', 10, 40)

  const texture2 = new THREE.CanvasTexture(canvas2)
  const spriteMat2 = new THREE.SpriteMaterial({ map: texture2 })
  const sprite2 = new THREE.Sprite(spriteMat2)
  sprite2.position.set(15, 14, -2)
  sprite2.scale.set(4, 1, 1)
  scene.add(sprite2)
}

function onResize() {
  const container = containerRef.value
  if (!container || !camera || !renderer) return
  camera.aspect = container.clientWidth / container.clientHeight
  camera.updateProjectionMatrix()
  renderer.setSize(container.clientWidth, container.clientHeight)
}

function handleDeviceStatus(data) {
  if (!crane || !Array.isArray(data)) return
  const craneStatus = data.find(d => d.type === 'Crane')
  if (craneStatus) {
    craneTarget.y = 3
  }
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
  if (renderer) {
    renderer.dispose()
    containerRef.value?.removeChild(renderer.domElement)
  }
  window.removeEventListener('resize', onResize)
})
</script>

<style scoped>
.monitor3d-container {
  width: 100%;
  height: 100%;
  min-height: calc(100vh - 90px);
  border-radius: 8px;
  overflow: hidden;
}
</style>
