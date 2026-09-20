import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { TodoDetailRoute } from '../../src/routes/TodoDetailRoute'
import { todoDetailLoader } from '../../src/routes/TodoDetailRoute.loader'
import { todoDetailAction } from '../../src/routes/TodoDetailRoute.action'
import { ErrorBoundary } from '../../src/routes/ErrorBoundary'
import { server } from '../mocks/server'
import { sampleTodo } from '../mocks/handlers'

const BASE_URL = import.meta.env.VITE_API_BASE_URL

const renderRoute = (id: string) => {
  const router = createMemoryRouter(
    [
      {
        path: '/',
        element: <p>List page</p>,
      },
      {
        path: '/todos/:id',
        element: <TodoDetailRoute />,
        loader: todoDetailLoader,
        action: todoDetailAction,
        errorElement: <ErrorBoundary />,
      },
    ],
    { initialEntries: [`/todos/${id}`] },
  )
  render(<RouterProvider router={router} />)
  return router
}

describe('TodoDetailRoute', () => {
  it('renders the todo details from the loader', async () => {
    renderRoute(sampleTodo.id)

    expect(await screen.findByText(sampleTodo.title)).toBeInTheDocument()
    expect(screen.getByText(sampleTodo.description!)).toBeInTheDocument()
  })

  it('renders the error boundary for a todo that does not exist', async () => {
    renderRoute('does-not-exist')

    expect(await screen.findByText('Todo not found')).toBeInTheDocument()
  })

  it('links back to the list', async () => {
    renderRoute(sampleTodo.id)

    await screen.findByText(sampleTodo.title)

    expect(screen.getByRole('link', { name: /back to list/i })).toHaveAttribute('href', '/')
  })

  it('shows the confirmation dialog when Delete is clicked, without submitting', async () => {
    const user = userEvent.setup()
    let deleteCalls = 0
    server.use(
      http.delete(`${BASE_URL}/todos/:id`, () => {
        deleteCalls += 1
        return new HttpResponse(null, { status: 204 })
      }),
    )

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    await user.click(screen.getByRole('button', { name: 'Delete' }))

    expect(screen.getByText('Delete this to-do?')).toBeInTheDocument()
    expect(screen.getByText(sampleTodo.title)).toBeInTheDocument()
    expect(deleteCalls).toBe(0)
  })

  it('closes the dialog without deleting when No is clicked', async () => {
    const user = userEvent.setup()
    let deleteCalls = 0
    server.use(
      http.delete(`${BASE_URL}/todos/:id`, () => {
        deleteCalls += 1
        return new HttpResponse(null, { status: 204 })
      }),
    )

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    await user.click(screen.getByRole('button', { name: 'Delete' }))
    await user.click(screen.getByRole('button', { name: 'No' }))

    expect(screen.queryByText('Delete this to-do?')).not.toBeInTheDocument()
    expect(deleteCalls).toBe(0)
  })

  it('deletes the todo and navigates back to the list when Yes is clicked', async () => {
    const user = userEvent.setup()

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    await user.click(screen.getByRole('button', { name: 'Delete' }))
    await user.click(screen.getByRole('button', { name: 'Yes' }))

    await waitFor(() => {
      expect(screen.getByText('List page')).toBeInTheDocument()
    })
  })

  it('navigates back to the list even if the todo was already deleted (404 race)', async () => {
    const user = userEvent.setup()
    server.use(http.delete(`${BASE_URL}/todos/:id`, () => new HttpResponse(null, { status: 404 })))

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    await user.click(screen.getByRole('button', { name: 'Delete' }))
    await user.click(screen.getByRole('button', { name: 'Yes' }))

    await waitFor(() => {
      expect(screen.getByText('List page')).toBeInTheDocument()
    })
  })

  it('shows a Complete button for an incomplete todo and toggles it to Incomplete', async () => {
    const user = userEvent.setup()

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    expect(screen.getByRole('button', { name: 'Complete' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Complete' }))

    expect(await screen.findByRole('button', { name: 'Incomplete' })).toBeInTheDocument()
    expect(screen.getByText('Completed')).toBeInTheDocument()
  })

  it('toggles a completed todo back to incomplete', async () => {
    const user = userEvent.setup()

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    await user.click(screen.getByRole('button', { name: 'Complete' }))
    expect(await screen.findByRole('button', { name: 'Incomplete' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Incomplete' }))

    expect(await screen.findByRole('button', { name: 'Complete' })).toBeInTheDocument()
    expect(screen.getByText('Incomplete')).toBeInTheDocument()
  })

  it('navigates back to the list if the todo was already deleted before toggling (404 race)', async () => {
    const user = userEvent.setup()
    server.use(http.patch(`${BASE_URL}/todos/:id`, () => new HttpResponse(null, { status: 404 })))

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    await user.click(screen.getByRole('button', { name: 'Complete' }))

    await waitFor(() => {
      expect(screen.getByText('List page')).toBeInTheDocument()
    })
  })
})
