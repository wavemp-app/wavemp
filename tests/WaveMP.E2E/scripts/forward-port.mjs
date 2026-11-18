// Forwards a local TCP port to another host, e.g. localhost:8080 in the Playwright container to
// the WaveMP instance on the Docker host (host.docker.internal:8080), so the pages keep the origin
// http://localhost:8080 that the app and the tests expect:
//   node scripts/forward-port.mjs <local port> <target host> <target port>

import net from 'node:net'

const [localPort, targetHost, targetPort] = process.argv.slice(2)
if (!localPort || !targetHost || !targetPort) {
    console.error('Usage: node scripts/forward-port.mjs <local port> <target host> <target port>')
    process.exit(1)
}

net.createServer((client) => {
    const target = net.connect(Number(targetPort), targetHost)
    client.pipe(target).pipe(client)
    client.on('error', () => target.destroy())
    target.on('error', () => client.destroy())
}).listen(Number(localPort), '127.0.0.1')
