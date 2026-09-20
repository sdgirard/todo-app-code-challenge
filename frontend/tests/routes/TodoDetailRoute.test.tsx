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

  it('shows the pre-filled edit form and hides Delete/toggle when Edit is clicked', async () => {
    const user = userEvent.setup()

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    await user.click(screen.getByRole('button', { name: 'Edit' }))

    expect(screen.getByLabelText('Title')).toHaveValue(sampleTodo.title)
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Complete' })).not.toBeInTheDocument()
  })

  it('hides the back-to-list link while editing, leaving Cancel as the only way back', async () => {
    const user = userEvent.setup()

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)
    expect(screen.getByRole('link', { name: /back to list/i })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Edit' }))

    expect(screen.queryByRole('link', { name: /back to list/i })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument()
  })

  it('returns to the read-only view with original values when Cancel is clicked, without submitting', async () => {
    const user = userEvent.setup()
    let putCalls = 0
    server.use(
      http.put(`${BASE_URL}/todos/:id`, () => {
        putCalls += 1
        return HttpResponse.json(sampleTodo)
      }),
    )

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    await user.click(screen.getByRole('button', { name: 'Edit' }))
    await user.type(screen.getByLabelText('Title'), '!')
    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.getByText(sampleTodo.title)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument()
    expect(putCalls).toBe(0)
  })

  it('saves an edited title and returns to the read-only view showing the new value', async () => {
    const user = userEvent.setup()

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    await user.click(screen.getByRole('button', { name: 'Edit' }))
    const titleInput = screen.getByLabelText('Title')
    await user.clear(titleInput)
    await user.type(titleInput, 'Buy oat milk')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Buy oat milk')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument()
  })

  it('keeps the form open and shows a field error when the server rejects the edit (400)', async () => {
    const user = userEvent.setup()
    server.use(
      http.put(`${BASE_URL}/todos/:id`, () =>
        HttpResponse.json(
          {
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: { Title: ["'Title' must not be empty."] },
          },
          { status: 400 },
        ),
      ),
    )

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    await user.click(screen.getByRole('button', { name: 'Edit' }))
    await user.type(screen.getByLabelText('Title'), '!')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText("'Title' must not be empty.")).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toBeInTheDocument()
  })

  it('navigates back to the list if the todo was already deleted before saving (404 race)', async () => {
    const user = userEvent.setup()
    server.use(http.put(`${BASE_URL}/todos/:id`, () => new HttpResponse(null, { status: 404 })))

    renderRoute(sampleTodo.id)
    await screen.findByText(sampleTodo.title)

    await user.click(screen.getByRole('button', { name: 'Edit' }))
    const titleInput = screen.getByLabelText('Title')
    await user.type(titleInput, '!')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => {
      expect(screen.getByText('List page')).toBeInTheDocument()
    })
  })
})
