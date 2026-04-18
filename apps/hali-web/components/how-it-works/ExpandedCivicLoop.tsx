import CivicLoopStage from './CivicLoopStage'
import WaveIcon from '@/components/icons/WaveIcon'
import PeopleIcon from '@/components/icons/PeopleIcon'
import BuildingIcon from '@/components/icons/BuildingIcon'
import CheckIcon from '@/components/icons/CheckIcon'

const STAGES = [
  {
    stageNumber: 1,
    title: 'You report',
    icon: <WaveIcon className="w-10 h-10" />,
    explanation:
      'Type what you see in plain language — no forms, no categories to select. Hali understands what you mean and extracts the location, condition, and civic category from your words. If something is unclear, it asks before assuming.',
    example:
      "A resident in Nairobi West types: 'Huge potholes on Lusaka Road near National Oil, very difficult to pass with the rains.' Hali identifies the location, category (roads), condition (difficult), and checks for nearby reports from others who saw the same thing.",
  },
  {
    stageNumber: 2,
    title: 'Signals cluster',
    icon: <PeopleIcon className="w-10 h-10" />,
    explanation:
      "Your report doesn't stand alone. Hali searches for nearby reports on the same condition and groups them into a single, visible civic reality. The more people report, the stronger and clearer the signal becomes. Clustering means one problem shows up as one thing — not fifty separate noise points.",
    example:
      "Three residents near Industrial Area report difficult road conditions within two hours. Hali groups them into one cluster: 'Pothole damage affecting Lusaka Road near CFAO Mobility.' One signal. Confirmed by multiple people. Traceable to a specific place.",
  },
  {
    stageNumber: 3,
    title: 'Institutions respond',
    icon: <BuildingIcon className="w-10 h-10" />,
    explanation:
      'When a condition is visible and confirmed, institutions — road authorities, utilities, county departments — see it structured and actionable. They post updates that citizens see directly alongside the original signal. Not instead of it. The response and the signal live side by side.',
    example:
      "The Kenya National Highways Authority sees the Lusaka Road cluster and posts: 'Teams dispatched to assess road surface damage near CFAO Mobility.' Citizens following the signal see the update immediately. The cluster stays visible until the condition is actually resolved.",
  },
  {
    stageNumber: 4,
    title: 'Resolution is confirmed',
    icon: <CheckIcon className="w-10 h-10" />,
    explanation:
      'When an institution posts that service is restored, Hali treats that as a proposal — not a fact. It asks the people who were affected: is this actually fixed for you? Resolution happens when enough of them say yes. If they don\u2019t, the signal stays active. This asymmetry is intentional — it protects the credibility of the system.',
    example:
      "Kenya Power posts 'Power restored in South B estate.' Hali sends prompts to residents who marked themselves as affected. When enough of them confirm, the cluster resolves. If residents are still reporting no power, it stays active — regardless of what the institution said.",
  },
]

export default function ExpandedCivicLoop() {
  return (
    <section className="bg-hali-background">
      <div className="max-w-5xl mx-auto px-6 pt-8">
        <h2 className="font-display text-3xl md:text-4xl font-bold text-hali-foreground">
          How the loop works
        </h2>
      </div>
      <div className="max-w-5xl mx-auto px-6">
        {STAGES.map((stage, i) => (
          <CivicLoopStage
            key={stage.stageNumber}
            stageNumber={stage.stageNumber}
            title={stage.title}
            icon={stage.icon}
            explanation={stage.explanation}
            example={stage.example}
            isLast={i === STAGES.length - 1}
          />
        ))}
      </div>
    </section>
  )
}
