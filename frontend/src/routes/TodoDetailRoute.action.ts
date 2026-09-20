import { redirect, type ActionFunctionArgs } from 'react-router'
import { deleteTodo, updateCompletionStatus } from '../api/generated/todos/todos'

export const todoDetailAction = async ({ params, request }: ActionFunctionArgs): Promise<Response | null> => {
  const id = params.id as string
  const formData = await request.formData()
  const intent = formData.get('intent')

  if (intent === 'delete') {
    // Both documented outcomes (204 deleted, 404 already gone) satisfy the
    // user's intent that this todo no longer exist — see spec's Action section.
    await deleteTodo(id)
    return redirect('/')
  }

  if (intent === 'toggleComplete') {
    const isCompleted = formData.get('isCompleted') === 'true'
    const result = await updateCompletionStatus(id, { isCompleted })
    const status: number = result.status

    if (status === 200) {
      return null
    }

    if (status === 404) {
      return redirect('/')
    }

    throw new Response('Failed to update completion status', { status })
  }

  throw new Response('Unknown action', { status: 400 })
}
