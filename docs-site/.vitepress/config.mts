import { defineConfig } from 'vitepress'

export default defineConfig({
  srcDir: 'content',
  title: 'SoulCore Handbook',
  description: 'Architecture, modules, workflows, and runbooks for SoulCore / House Victoria',
  cleanUrls: true,
  ignoreDeadLinks: true,
  themeConfig: {
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Architecture', link: '/handbook/architecture/overview' },
      { text: 'Modules', link: '/handbook/modules' },
      { text: 'Workflows', link: '/handbook/workflows/allstart' },
      { text: 'Conventions', link: '/handbook/conventions' },
      { text: 'SMS runbook', link: '/runbooks/sms-gateway-inbound' },
    ],
    sidebar: [
      {
        text: 'Handbook',
        items: [
          { text: 'Overview', link: '/handbook/' },
          { text: 'Modules', link: '/handbook/modules' },
          { text: 'Conventions', link: '/handbook/conventions' },
          { text: 'Glossary', link: '/handbook/glossary' },
        ],
      },
      {
        text: 'Architecture',
        items: [
          { text: 'Overview', link: '/handbook/architecture/overview' },
          { text: 'Host & protocol', link: '/handbook/architecture/host-protocol' },
          { text: 'Inference & tools', link: '/handbook/architecture/inference-tools' },
          { text: 'Memory & charter', link: '/handbook/architecture/memory-charter' },
          { text: 'Embodiment (UE)', link: '/handbook/architecture/embodiment' },
          { text: 'Clients', link: '/handbook/architecture/clients' },
          { text: 'Security & network', link: '/handbook/architecture/security-network' },
        ],
      },
      {
        text: 'Workflows',
        items: [
          { text: 'ALLSTART desk stack', link: '/handbook/workflows/allstart' },
          { text: 'SMS / MMS gateway', link: '/handbook/workflows/sms-gateway' },
          { text: 'Presence chat', link: '/handbook/workflows/presence-chat' },
        ],
      },
      {
        text: 'Runbooks',
        items: [
          { text: 'SMS gateway', link: '/runbooks/sms-gateway-inbound' },
          { text: 'Tailscale serve', link: '/runbooks/tailscale-serve-soulcore' },
          { text: 'Cursor My Machines', link: '/runbooks/cursor-my-machines' },
          { text: 'Victoria email', link: '/runbooks/victoria-email' },
          { text: 'Kayleigh player pawn', link: '/runbooks/kayleigh-player-pawn-setup' },
          { text: 'ACE hook fix', link: '/runbooks/cursor-ace-hook-fix' },
        ],
      },
      {
        text: 'Process',
        items: [
          { text: 'PROP numbering', link: '/agents/PROP_NUMBERING' },
        ],
      },
    ],
    search: {
      provider: 'local',
    },
    outline: { level: [2, 3] },
    socialLinks: [],
  },
})
